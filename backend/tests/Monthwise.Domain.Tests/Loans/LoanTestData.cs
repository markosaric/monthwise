using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Domain.Tests.Loans;

internal static class LoanTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    public static readonly Guid AccountId = Guid.NewGuid();

    public static LoanDetails Details(
        PaymentMethod method = PaymentMethod.StandingOrder,
        YearMonth? start = null,
        int paymentDay = 15,
        long monthlyCents = 50_000,
        int count = 12,
        long? residualCents = null,
        string? name = "Car loan",
        Guid? accountId = null) =>
        new(
            name,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            method,
            method == PaymentMethod.StandingOrder ? accountId ?? AccountId : null,
            start ?? new YearMonth(2026, 8),
            paymentDay,
            Money.FromCents(monthlyCents),
            count,
            residualCents is { } cents ? Money.FromCents(cents) : null,
            "Contract 1");

    public static Loan NewLoan(LoanDetails? details = null, bool residualAllowed = true) =>
        Loan.Create(details ?? Details(), residualAllowed, Now).Value;
}
