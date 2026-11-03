namespace Monthwise.Domain.Cards;

public enum CardType
{
    Debit,
    Charge,
    Credit,
    Prepaid,

    /// <summary>A revolving credit card: a debt paid back with a monthly minimum payment (revolving FR-1).</summary>
    Revolving,
}
