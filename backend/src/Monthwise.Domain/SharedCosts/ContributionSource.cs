using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Domain.SharedCosts;

/// <summary>What a contribution is paid back for (FR-5).</summary>
public enum ContributionSourceKind
{
    Bill,
    LoanPayment,
}

/// <summary>
/// Identifies the shared cost of a contribution: one bill, or one loan payment (loan, scheduled month and
/// kind). Every shared cost has at most one contribution.
/// </summary>
public sealed record ContributionSource(
    ContributionSourceKind Kind,
    Guid? BillId,
    Guid? LoanId,
    YearMonth? Month,
    LoanPaymentKind? PaymentKind)
{
    public static ContributionSource ForBill(Guid billId) => new(ContributionSourceKind.Bill, billId, null, null, null);

    public static ContributionSource ForLoanPayment(Guid loanId, YearMonth month, LoanPaymentKind kind) =>
        new(ContributionSourceKind.LoanPayment, null, loanId, month, kind);
}

/// <summary>
/// The values of a contribution calculated from its shared cost (FR-5). <see cref="AccountId"/> is null
/// when no account applies yet; the user then chooses one.
/// </summary>
public sealed record ContributionValues(
    Guid PayerId,
    Money Amount,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    Guid? AccountId,
    string Description)
{
    /// <summary>The predefined input category of contributions, Agreed contributions (input cash flow FR-6).</summary>
    public static readonly Guid AgreedContributionsCategoryId = new("0199b0c0-0000-7000-8000-000000000203");

    /// <summary>
    /// A contribution for a cost of <paramref name="amount"/>: the participant's part, with the cost's
    /// dates. The account is the one chosen on the share, otherwise <paramref name="costAccountId"/> (what
    /// the cost is paid from when it is an account), otherwise <paramref name="onlyPrimaryAccountId"/>.
    /// </summary>
    public static ContributionValues Calculate(
        CostShare share,
        Guid payerId,
        Money amount,
        DateOnly issueDate,
        DateOnly dueDate,
        Guid? costAccountId,
        Guid? onlyPrimaryAccountId,
        string description)
    {
        ArgumentNullException.ThrowIfNull(share);
        return new ContributionValues(
            payerId,
            share.PartOf(amount),
            issueDate,
            dueDate < issueDate ? issueDate : dueDate,
            share.ContributionAccountId ?? costAccountId ?? onlyPrimaryAccountId,
            description);
    }
}
