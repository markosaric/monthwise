using Monthwise.Domain.Common;

namespace Monthwise.Domain.Lendings;

/// <summary>What the user enters for a repayment (money lending R1 to R4).</summary>
public sealed record RepaymentDetails(Money Amount, Guid ToAccountId, DateOnly ReceivedDate, string? Note);

/// <summary>
/// An amount the borrower returned (money lending FR-4): an input of <see cref="ToAccountId"/> on
/// <see cref="ReceivedDate"/>. Created and changed only through its <see cref="Lending"/>, which checks
/// that the repayments never exceed the lent amount.
/// </summary>
public sealed class LendingRepayment
{
    public const int NoteMaxLength = 200;

    private LendingRepayment()
    {
    }

    public Guid Id { get; private set; }
    public Guid LendingId { get; private set; }
    public Money Amount { get; private set; }
    public Guid ToAccountId { get; private set; }
    public DateOnly ReceivedDate { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public RepaymentDetails Details => new(Amount, ToAccountId, ReceivedDate, Note);

    internal static LendingRepayment Create(Guid lendingId, RepaymentDetails details, DateTimeOffset now)
    {
        var repayment = new LendingRepayment { Id = Guid.CreateVersion7(now), LendingId = lendingId, CreatedAt = now };
        repayment.Apply(details, now);
        return repayment;
    }

    internal void Apply(RepaymentDetails details, DateTimeOffset now)
    {
        Amount = details.Amount;
        ToAccountId = details.ToAccountId;
        ReceivedDate = details.ReceivedDate;
        Note = Lending.NormalizeText(details.Note);
        UpdatedAt = now;
    }
}
