using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Domain.Cards;

/// <summary>
/// What the user enters for a revolving card (revolving FR-2, R1 to R7). Percentages are whole basis
/// points: 8.33 % is 833. The paying account must be a usable account; the caller checks that.
/// </summary>
public sealed record RevolvingDetails(
    int MinPaymentBasisPoints,
    Money MinPaymentAmount,
    int InterestBasisPoints,
    int? MinPaymentInterestBasisPoints,
    int BillDay,
    int DueDay,
    Guid PayingAccountId,
    bool PaysFromAccountDirectly);

/// <summary>
/// The minimum payment, interest and bill days of a revolving card from <see cref="FromMonth"/> on
/// (revolving FR-2). A card keeps every version, so a change applies from the next monthly bill and
/// bills already made keep their values.
/// </summary>
public sealed class RevolvingTerms
{
    public const int MinDay = 1;
    public const int MaxDay = 31;

    /// <summary>100 % in basis points.</summary>
    public const int FullBasisPoints = 10_000;

    private RevolvingTerms()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public YearMonth FromMonth { get; private set; }

    /// <summary>R1: the part of the debt to pay each month, in basis points.</summary>
    public int MinPaymentBasisPoints { get; private set; }

    /// <summary>R2: the lowest minimum payment.</summary>
    public Money MinPaymentAmount { get; private set; }

    /// <summary>R3: the yearly regular interest rate, in basis points.</summary>
    public int InterestBasisPoints { get; private set; }

    /// <summary>R3a: the part of R1 that is interest, as the bank states it. For information only.</summary>
    public int? MinPaymentInterestBasisPoints { get; private set; }

    /// <summary>R4: the day of the month the monthly bill is made.</summary>
    public int BillDay { get; private set; }

    /// <summary>R5: the day of the month the monthly bill is due; in the next month when not later than the bill day.</summary>
    public int DueDay { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static RevolvingTerms Create(Guid cardId, YearMonth fromMonth, RevolvingDetails details, DateTimeOffset now)
    {
        var terms = new RevolvingTerms { Id = Guid.CreateVersion7(now), CardId = cardId, FromMonth = fromMonth, CreatedAt = now };
        terms.Change(details);
        return terms;
    }

    internal bool HasValues(RevolvingDetails details) =>
        MinPaymentBasisPoints == details.MinPaymentBasisPoints
        && MinPaymentAmount == details.MinPaymentAmount
        && InterestBasisPoints == details.InterestBasisPoints
        && MinPaymentInterestBasisPoints == details.MinPaymentInterestBasisPoints
        && BillDay == details.BillDay
        && DueDay == details.DueDay;

    internal void Change(RevolvingDetails details)
    {
        MinPaymentBasisPoints = details.MinPaymentBasisPoints;
        MinPaymentAmount = details.MinPaymentAmount;
        InterestBasisPoints = details.InterestBasisPoints;
        MinPaymentInterestBasisPoints = details.MinPaymentInterestBasisPoints;
        BillDay = details.BillDay;
        DueDay = details.DueDay;
    }

    /// <summary>Checks R1 to R5 (revolving FR-2).</summary>
    internal static void Validate(RevolvingDetails? details, List<DomainError> errors)
    {
        if (details is null)
        {
            errors.Add(CardErrors.RevolvingRequired);
            return;
        }
        var minPaymentValid = details.MinPaymentBasisPoints is > 0 and <= FullBasisPoints;
        if (!minPaymentValid)
        {
            errors.Add(CardErrors.MinPaymentPercentInvalid);
        }
        if (details.MinPaymentAmount.IsNegative)
        {
            errors.Add(CardErrors.MinPaymentAmountNegative);
        }
        if (details.InterestBasisPoints < 0)
        {
            errors.Add(CardErrors.InterestRateNegative);
        }
        if (details.MinPaymentInterestBasisPoints is { } interestPart
            && (interestPart < 0 || (minPaymentValid && interestPart > details.MinPaymentBasisPoints)))
        {
            errors.Add(CardErrors.MinPaymentInterestInvalid);
        }
        if (details.BillDay is < MinDay or > MaxDay)
        {
            errors.Add(CardErrors.BillDayInvalid);
        }
        if (details.DueDay is < MinDay or > MaxDay)
        {
            errors.Add(CardErrors.DueDayInvalid);
        }
        if (details.PayingAccountId == Guid.Empty)
        {
            errors.Add(CardErrors.RevolvingPayingAccountRequired);
        }
    }
}

/// <summary>
/// The bill and due dates of a revolving card for every month (revolving FR-2). The terms of a month
/// are the newest ones that start in or before it.
/// </summary>
public sealed class RevolvingCalendar
{
    private readonly List<RevolvingTerms> _terms;

    public RevolvingCalendar(IEnumerable<RevolvingTerms> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        _terms = terms.OrderBy(t => t.FromMonth).ToList();
        if (_terms.Count == 0)
        {
            throw new ArgumentException("A revolving calendar needs at least one version of the terms.", nameof(terms));
        }
    }

    /// <summary>The terms that apply to the bill of <paramref name="month"/>.</summary>
    public RevolvingTerms TermsFor(YearMonth month) => _terms.LastOrDefault(t => t.FromMonth <= month) ?? _terms[0];

    /// <summary>The bill date of <paramref name="month"/>: the bill day, clamped to the end of a shorter month.</summary>
    public DateOnly BillDate(YearMonth month)
    {
        var day = TermsFor(month).BillDay;
        return new DateOnly(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
    }

    /// <summary>
    /// The due date of the bill of <paramref name="month"/>: the due day in the same month when it is
    /// later than the bill day, otherwise in the next month. A Saturday or Sunday moves to the following
    /// Monday, unless that Monday is in the next month.
    /// </summary>
    public DateOnly DueDate(YearMonth month)
    {
        var terms = TermsFor(month);
        var dueMonth = terms.DueDay > terms.BillDay ? month : month.Next();
        return DueDateRule.DueDate(dueMonth, terms.DueDay, PaymentMethod.Bill);
    }
}
