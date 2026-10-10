using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Loans;

/// <summary>The attributes of a loan the user enters (A1 to A13).</summary>
public sealed record LoanDetails(
    string? Name,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid LenderId,
    PaymentMethod PaymentMethod,
    Guid? AccountId,
    YearMonth StartMonth,
    int PaymentDay,
    Money MonthlyPayment,
    int PaymentCount,
    Money? ResidualValue,
    string? Description)
{
    /// <summary>The month of the last regular payment (A11).</summary>
    public YearMonth EndMonth => StartMonth.AddMonths(Math.Clamp(PaymentCount, 1, Loan.MaxPaymentCount) - 1);
}

/// <summary>
/// A bank loan or a lease (requirements: docs/feature-requirements/loans-and-leases.md). It is stored
/// once; its payments are calculated by <see cref="LoanSchedule"/> and stored as a
/// <see cref="LoanPayment"/> only when the user changes or pays one.
/// </summary>
public sealed class Loan
{
    public const int NameMaxLength = 50;
    public const int DescriptionMaxLength = 200;

    /// <summary>An upper bound that keeps every payment month valid: 100 years of monthly payments.</summary>
    public const int MaxPaymentCount = 1200;

    private const int MaxStartYear = 9998 - MaxPaymentCount / 12;

    private readonly List<PaymentChange> _changes = [];

    private Loan()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid SubcategoryId { get; private set; }
    public Guid LenderId { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public Guid? AccountId { get; private set; }
    public YearMonth StartMonth { get; private set; }
    public int PaymentDay { get; private set; }
    public Money MonthlyPayment { get; private set; }
    public int PaymentCount { get; private set; }
    public Money? ResidualValue { get; private set; }
    public string? Description { get; private set; }
    public DateOnly? ClosedOn { get; private set; }

    /// <summary>The share columns of <see cref="Share"/> (FR-2); null when the loan is not shared.</summary>
    public Guid? ShareParticipantId { get; private set; }
    public int? ShareCount { get; private set; }
    public int? ParticipantShareCount { get; private set; }
    public Guid? ContributionAccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Payment changes, oldest month first (FR-7).</summary>
    public IReadOnlyList<PaymentChange> Changes => _changes.OrderBy(c => c.FromMonth).ToList();

    public bool IsClosed => ClosedOn is not null;

    /// <summary>How the loan is shared (shared bills FR-3): applies to every payment that is not paid and has no share of its own.</summary>
    public CostShare? Share => CostShareColumns.Read(ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId);

    /// <summary>The month of the last regular payment (A11).</summary>
    public YearMonth EndMonth => StartMonth.AddMonths(PaymentCount - 1);

    /// <summary>The month of the last payment: the residual value's month when there is one (FR-6).</summary>
    public YearMonth LastPaymentMonth => ResidualValue is null ? EndMonth : EndMonth.Next();

    public LoanDetails Details => new(
        Name, CategoryId, SubcategoryId, LenderId, PaymentMethod, AccountId, StartMonth, PaymentDay,
        MonthlyPayment, PaymentCount, ResidualValue, Description);

    /// <summary>Number of payments from <paramref name="start"/> to <paramref name="end"/>, both included (A10, A11).</summary>
    public static int PaymentCountBetween(YearMonth start, YearMonth end) => start.MonthsUntil(end) + 1;

    /// <summary>
    /// Adds a loan. <paramref name="residualValueAllowed"/> says whether the chosen category allows a
    /// residual value (FR-8); references to other entities are checked by the caller.
    /// </summary>
    public static Result<Loan> Create(LoanDetails details, bool residualValueAllowed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, residualValueAllowed);
        if (errors.Count > 0)
        {
            return Result.Failure<Loan>(errors);
        }
        var loan = new Loan { Id = Guid.CreateVersion7(now), CreatedAt = now };
        loan.Apply(details, now);
        return Result.Success(loan);
    }

    /// <summary>
    /// Edits the loan (FR-10). <paramref name="paid"/> describes the payments already paid: the
    /// changed loan must still contain them, so they stay unchanged.
    /// </summary>
    public Result Update(LoanDetails details, bool residualValueAllowed, PaidPayments paid, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(paid);
        if (IsClosed)
        {
            return Result.Failure(LoanErrors.LoanClosed);
        }
        // A residual value the loan already has stays allowed when the category is later unticked (FR-8).
        var errors = Validate(details, residualValueAllowed || ResidualValue is not null);
        if (errors.Count == 0)
        {
            if (paid.EarliestRegularMonth is { } earliest && details.StartMonth > earliest)
            {
                errors.Add(LoanErrors.StartAfterPaidPayment);
            }
            if (paid.LatestRegularMonth is { } latest && details.EndMonth < latest)
            {
                errors.Add(LoanErrors.EndBeforePaidPayment);
            }
            if (paid.ResidualPaid && (details.ResidualValue != ResidualValue || details.EndMonth != EndMonth))
            {
                errors.Add(LoanErrors.ResidualValuePaid);
            }
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    /// <summary>Shares the loan, changes its share or stops sharing it (shared bills FR-3).</summary>
    public Result SetShare(CostShare? share, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return Result.Failure(LoanErrors.LoanClosed);
        }
        (ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId) = CostShareColumns.Write(share);
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Closes the loan early (FR-10). Its expected payments are removed; closing cannot be undone.</summary>
    public Result Close(DateOnly today, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return Result.Failure(LoanErrors.LoanClosed);
        }
        ClosedOn = today;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Records a payment change (FR-7). <paramref name="latestPaidMonth"/> is the month of the latest paid payment.</summary>
    public Result<PaymentChange> AddChange(PaymentChangeDetails details, YearMonth? latestPaidMonth, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = ValidateChange(details, latestPaidMonth, ignoreChangeId: null);
        if (errors.Count > 0)
        {
            return Result.Failure<PaymentChange>(errors);
        }
        var change = PaymentChange.Create(Id, details, now);
        _changes.Add(change);
        UpdatedAt = now;
        return Result.Success(change);
    }

    /// <summary>Edits a payment change, while none of the payments it applies to is paid (FR-7).</summary>
    public Result UpdateChange(Guid changeId, PaymentChangeDetails details, YearMonth? latestPaidMonth, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var change = _changes.Find(c => c.Id == changeId);
        if (change is null)
        {
            return Result.Failure(LoanErrors.ChangeNotFound);
        }
        if (CoversPaidPayment(change, latestPaidMonth))
        {
            return Result.Failure(LoanErrors.ChangeCoversPaidPayment);
        }
        var errors = ValidateChange(details, latestPaidMonth, changeId);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        change.Apply(details, now);
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Deletes a payment change, while none of the payments it applies to is paid (FR-7).</summary>
    public Result RemoveChange(Guid changeId, YearMonth? latestPaidMonth, DateTimeOffset now)
    {
        var change = _changes.Find(c => c.Id == changeId);
        if (change is null)
        {
            return Result.Failure(LoanErrors.ChangeNotFound);
        }
        if (IsClosed)
        {
            return Result.Failure(LoanErrors.LoanClosed);
        }
        if (CoversPaidPayment(change, latestPaidMonth))
        {
            return Result.Failure(LoanErrors.ChangeCoversPaidPayment);
        }
        _changes.Remove(change);
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>The monthly payment that applies to <paramref name="month"/>: from the newest change that sets one (FR-7).</summary>
    public Money MonthlyPaymentFor(YearMonth month) =>
        _changes.Where(c => c.FromMonth <= month && c.MonthlyPayment is not null)
            .OrderBy(c => c.FromMonth)
            .LastOrDefault()?.MonthlyPayment ?? MonthlyPayment;

    /// <summary>The payment day that applies to <paramref name="month"/>: from the newest change that sets one (FR-7).</summary>
    public int PaymentDayFor(YearMonth month) =>
        _changes.Where(c => c.FromMonth <= month && c.PaymentDay is not null)
            .OrderBy(c => c.FromMonth)
            .LastOrDefault()?.PaymentDay ?? PaymentDay;

    public LoanSchedule Schedule(IEnumerable<LoanPayment> storedPayments, DateOnly? chargesStopOn = null) =>
        new(this, storedPayments, chargesStopOn);

    internal static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool CoversPaidPayment(PaymentChange change, YearMonth? latestPaidMonth) =>
        latestPaidMonth is { } latest && latest >= change.FromMonth;

    private List<DomainError> ValidateChange(PaymentChangeDetails details, YearMonth? latestPaidMonth, Guid? ignoreChangeId)
    {
        if (IsClosed)
        {
            return [LoanErrors.LoanClosed];
        }
        var errors = PaymentChange.Validate(details);
        if (latestPaidMonth is { } latest && details.FromMonth <= latest)
        {
            errors.Add(LoanErrors.ChangeNotAfterPaid);
        }
        else if (details.FromMonth < StartMonth || details.FromMonth > LastPaymentMonth)
        {
            errors.Add(LoanErrors.ChangeOutsideLoan);
        }
        if (_changes.Any(c => c.Id != ignoreChangeId && c.FromMonth == details.FromMonth))
        {
            errors.Add(LoanErrors.ChangeMonthInUse);
        }
        return errors;
    }

    private static List<DomainError> Validate(LoanDetails details, bool residualValueAllowed)
    {
        var errors = new List<DomainError>();
        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add(LoanErrors.NameRequired);
        }
        else if (name.Length > NameMaxLength)
        {
            errors.Add(LoanErrors.NameTooLong);
        }
        if (details.CategoryId == Guid.Empty)
        {
            errors.Add(LoanErrors.CategoryRequired);
        }
        if (details.SubcategoryId == Guid.Empty)
        {
            errors.Add(LoanErrors.SubcategoryRequired);
        }
        if (details.LenderId == Guid.Empty)
        {
            errors.Add(LoanErrors.LenderRequired);
        }
        if (!Enum.IsDefined(details.PaymentMethod))
        {
            errors.Add(LoanErrors.PaymentMethodRequired);
        }
        else if (details.PaymentMethod == PaymentMethod.StandingOrder && (details.AccountId ?? Guid.Empty) == Guid.Empty)
        {
            errors.Add(LoanErrors.AccountRequired);
        }
        else if (details.PaymentMethod == PaymentMethod.Bill && details.AccountId is not null)
        {
            errors.Add(LoanErrors.AccountNotForBill);
        }
        if (details.StartMonth.Year > MaxStartYear)
        {
            errors.Add(LoanErrors.StartMonthInvalid);
        }
        if (details.PaymentDay is < DueDateRule.MinPaymentDay or > DueDateRule.MaxPaymentDay)
        {
            errors.Add(LoanErrors.PaymentDayInvalid);
        }
        if (!details.MonthlyPayment.IsPositive)
        {
            errors.Add(LoanErrors.MonthlyPaymentNotPositive);
        }
        if (details.PaymentCount < 1)
        {
            errors.Add(LoanErrors.PaymentCountInvalid);
        }
        else if (details.PaymentCount > MaxPaymentCount)
        {
            errors.Add(LoanErrors.PaymentCountTooLarge);
        }
        if (details.ResidualValue is { } residual)
        {
            if (!residual.IsPositive)
            {
                errors.Add(LoanErrors.ResidualValueNotPositive);
            }
            else if (!residualValueAllowed)
            {
                errors.Add(LoanErrors.ResidualValueNotAllowed);
            }
        }
        if (NormalizeText(details.Description) is { Length: > DescriptionMaxLength })
        {
            errors.Add(LoanErrors.DescriptionTooLong);
        }
        return errors;
    }

    private void Apply(LoanDetails details, DateTimeOffset now)
    {
        Name = details.Name!.Trim();
        CategoryId = details.CategoryId;
        SubcategoryId = details.SubcategoryId;
        LenderId = details.LenderId;
        PaymentMethod = details.PaymentMethod;
        AccountId = details.PaymentMethod == PaymentMethod.StandingOrder ? details.AccountId : null;
        StartMonth = details.StartMonth;
        PaymentDay = details.PaymentDay;
        MonthlyPayment = details.MonthlyPayment;
        PaymentCount = details.PaymentCount;
        ResidualValue = details.ResidualValue;
        Description = NormalizeText(details.Description);
        UpdatedAt = now;
    }
}

/// <summary>What is already paid on a loan, for the edit rules (FR-10).</summary>
public sealed record PaidPayments(YearMonth? EarliestRegularMonth, YearMonth? LatestRegularMonth, bool ResidualPaid)
{
    public static readonly PaidPayments None = new(null, null, false);
}
