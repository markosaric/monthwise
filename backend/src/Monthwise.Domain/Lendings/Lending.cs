using Monthwise.Domain.Common;

namespace Monthwise.Domain.Lendings;

/// <summary>What the user enters for a lending (money lending L1 to L6); the borrower is a payer.</summary>
public sealed record LendingDetails(
    Guid PayerId, Money Amount, DateOnly LendDate, Guid FromAccountId, DateOnly ExpectedReturnDate, string? Description);

/// <summary>
/// Money lent to a borrower once, to be returned later (money lending, *posudba*). The lent amount is an
/// output of <see cref="FromAccountId"/> on <see cref="LendDate"/>; each repayment is an input of its own
/// account; the outstanding amount is expected back on <see cref="ExpectedReturnDate"/>. The lending owns
/// its repayments, so the rules that span them (never more than lent, never before the lend date) are
/// checked here. Accounts are checked by the caller, which loads them.
/// </summary>
public sealed class Lending
{
    public const int DescriptionMaxLength = 200;

    private readonly List<LendingRepayment> _repayments = [];

    private Lending()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The borrower: a payer from the payers list (FR-6).</summary>
    public Guid PayerId { get; private set; }

    public Money Amount { get; private set; }
    public DateOnly LendDate { get; private set; }
    public Guid FromAccountId { get; private set; }
    public DateOnly ExpectedReturnDate { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Repayments, oldest first.</summary>
    public IReadOnlyList<LendingRepayment> Repayments =>
        _repayments.OrderBy(r => r.ReceivedDate).ThenBy(r => r.CreatedAt).ToList();

    public LendingDetails Details => new(PayerId, Amount, LendDate, FromAccountId, ExpectedReturnDate, Description);

    /// <summary>The sum of the repayments.</summary>
    public Money Returned => Money.Sum(_repayments.Select(r => r.Amount));

    /// <summary>The lent amount minus all repayments: what the borrower still owes (FR-4).</summary>
    public Money Outstanding => Amount - Returned;

    /// <summary>Returned when nothing is outstanding, Overdue when the expected return date is before today (FR-4, FR-5).</summary>
    public LendingState StateOn(DateOnly today) =>
        !Outstanding.IsPositive ? LendingState.Returned
        : ExpectedReturnDate < today ? LendingState.Overdue
        : LendingState.Open;

    public static Result<Lending> Create(LendingDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, returned: Money.Zero, firstRepayment: null);
        if (errors.Count > 0)
        {
            return Result.Failure<Lending>(errors);
        }
        var lending = new Lending { Id = Guid.CreateVersion7(now), CreatedAt = now };
        lending.Apply(details, now);
        return Result.Success(lending);
    }

    /// <summary>Edits the lending (FR-3): the amount stays at least what was returned, the lend date not after a repayment.</summary>
    public Result Update(LendingDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, Returned, _repayments.Count == 0 ? null : _repayments.Min(r => r.ReceivedDate));
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    /// <summary>Records a repayment (FR-4): not more than the outstanding amount, not before the lend date.</summary>
    public Result<LendingRepayment> AddRepayment(RepaymentDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = ValidateRepayment(details, Outstanding);
        if (errors.Count > 0)
        {
            return Result.Failure<LendingRepayment>(errors);
        }
        var repayment = LendingRepayment.Create(Id, details, now);
        _repayments.Add(repayment);
        UpdatedAt = now;
        return Result.Success(repayment);
    }

    /// <summary>Edits a repayment; its own amount counts as available again.</summary>
    public Result UpdateRepayment(Guid repaymentId, RepaymentDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var repayment = _repayments.Find(r => r.Id == repaymentId);
        if (repayment is null)
        {
            return Result.Failure(LendingErrors.RepaymentNotFound);
        }
        var errors = ValidateRepayment(details, Outstanding + repayment.Amount);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        repayment.Apply(details, now);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result RemoveRepayment(Guid repaymentId, DateTimeOffset now)
    {
        var repayment = _repayments.Find(r => r.Id == repaymentId);
        if (repayment is null)
        {
            return Result.Failure(LendingErrors.RepaymentNotFound);
        }
        _repayments.Remove(repayment);
        UpdatedAt = now;
        return Result.Success();
    }

    public LendingRepayment? FindRepayment(Guid repaymentId) => _repayments.Find(r => r.Id == repaymentId);

    internal static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<DomainError> Validate(LendingDetails details, Money returned, DateOnly? firstRepayment)
    {
        var errors = new List<DomainError>();
        if (!details.Amount.IsPositive)
        {
            errors.Add(LendingErrors.AmountNotPositive);
        }
        else if (details.Amount < returned)
        {
            errors.Add(LendingErrors.AmountBelowReturned);
        }
        if (details.FromAccountId == Guid.Empty)
        {
            errors.Add(LendingErrors.FromAccountRequired);
        }
        if (details.ExpectedReturnDate < details.LendDate)
        {
            errors.Add(LendingErrors.ReturnBeforeLend);
        }
        if (firstRepayment is { } first && details.LendDate > first)
        {
            errors.Add(LendingErrors.LendAfterRepayment);
        }
        if (NormalizeText(details.Description)?.Length > DescriptionMaxLength)
        {
            errors.Add(LendingErrors.DescriptionTooLong);
        }
        return errors;
    }

    private List<DomainError> ValidateRepayment(RepaymentDetails details, Money available)
    {
        var errors = new List<DomainError>();
        if (!details.Amount.IsPositive)
        {
            errors.Add(LendingErrors.RepaymentAmountNotPositive);
        }
        else if (details.Amount > available)
        {
            errors.Add(LendingErrors.RepaymentAboveOutstanding);
        }
        if (details.ToAccountId == Guid.Empty)
        {
            errors.Add(LendingErrors.ToAccountRequired);
        }
        if (details.ReceivedDate < LendDate)
        {
            errors.Add(LendingErrors.ReceivedBeforeLend);
        }
        if (NormalizeText(details.Note)?.Length > LendingRepayment.NoteMaxLength)
        {
            errors.Add(LendingErrors.NoteTooLong);
        }
        return errors;
    }

    private void Apply(LendingDetails details, DateTimeOffset now)
    {
        PayerId = details.PayerId;
        Amount = details.Amount;
        LendDate = details.LendDate;
        FromAccountId = details.FromAccountId;
        ExpectedReturnDate = details.ExpectedReturnDate;
        Description = NormalizeText(details.Description);
        UpdatedAt = now;
    }
}
