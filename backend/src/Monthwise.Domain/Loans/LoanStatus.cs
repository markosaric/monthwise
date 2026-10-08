namespace Monthwise.Domain.Loans;

/// <summary>The status of a loan on a given day (FR-10).</summary>
public enum LoanStatus
{
    /// <summary>Has expected payments.</summary>
    Active,

    /// <summary>All payments, the residual value included, are paid.</summary>
    PaidOff,

    /// <summary>Closed early by the user; only its paid payments remain.</summary>
    Closed,
}
