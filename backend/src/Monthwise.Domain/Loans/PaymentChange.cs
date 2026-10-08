using Monthwise.Domain.Common;

namespace Monthwise.Domain.Loans;

/// <summary>The values of a payment change the user enters (FR-7).</summary>
public sealed record PaymentChangeDetails(YearMonth FromMonth, Money? MonthlyPayment, int? PaymentDay, string? Note);

/// <summary>
/// A new monthly payment, a new payment day, or both, from <see cref="FromMonth"/> on, e.g. after the
/// bank changes the interest rate (FR-7). The interest rate itself is not recorded.
/// </summary>
public sealed class PaymentChange
{
    public const int NoteMaxLength = 200;

    private PaymentChange()
    {
    }

    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public YearMonth FromMonth { get; private set; }
    public Money? MonthlyPayment { get; private set; }
    public int? PaymentDay { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PaymentChangeDetails Details => new(FromMonth, MonthlyPayment, PaymentDay, Note);

    internal static PaymentChange Create(Guid loanId, PaymentChangeDetails details, DateTimeOffset now)
    {
        var change = new PaymentChange { Id = Guid.CreateVersion7(now), LoanId = loanId, CreatedAt = now };
        change.Apply(details, now);
        return change;
    }

    internal static List<DomainError> Validate(PaymentChangeDetails details)
    {
        var errors = new List<DomainError>();
        if (details.MonthlyPayment is null && details.PaymentDay is null)
        {
            errors.Add(LoanErrors.ChangeNothingChanged);
        }
        if (details.MonthlyPayment is { IsPositive: false })
        {
            errors.Add(LoanErrors.ChangeMonthlyPaymentNotPositive);
        }
        if (details.PaymentDay is < DueDateRule.MinPaymentDay or > DueDateRule.MaxPaymentDay)
        {
            errors.Add(LoanErrors.ChangePaymentDayInvalid);
        }
        if (Loan.NormalizeText(details.Note) is { Length: > NoteMaxLength })
        {
            errors.Add(LoanErrors.ChangeNoteTooLong);
        }
        return errors;
    }

    internal void Apply(PaymentChangeDetails details, DateTimeOffset now)
    {
        FromMonth = details.FromMonth;
        MonthlyPayment = details.MonthlyPayment;
        PaymentDay = details.PaymentDay;
        Note = Loan.NormalizeText(details.Note);
        UpdatedAt = now;
    }
}
