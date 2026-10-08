namespace Monthwise.Domain.Loans;

/// <summary>How the payments of a loan are paid (A5).</summary>
public enum PaymentMethod
{
    /// <summary>Charged automatically from a primary account on the due date (<i>trajni nalog</i>).</summary>
    StandingOrder,

    /// <summary>Paid by the user, like a regular bill (<i>uplatnica</i>), and ticked as paid.</summary>
    Bill,
}
