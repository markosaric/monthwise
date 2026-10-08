namespace Monthwise.Domain.Loans;

/// <summary>A monthly payment, or the residual value of a lease paid once after the last payment (FR-6).</summary>
public enum LoanPaymentKind
{
    Regular,
    Residual,
}
