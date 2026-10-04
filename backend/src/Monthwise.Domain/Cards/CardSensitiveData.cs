namespace Monthwise.Domain.Cards;

/// <summary>
/// The full card number and IBAN of a card. Kept apart from <see cref="Card"/> so they are only
/// loaded (and decrypted) when they are really needed: on reveal and on edit.
/// </summary>
public sealed class CardSensitiveData
{
    private CardSensitiveData()
    {
    }

    internal CardSensitiveData(Guid cardId, CardNumber? cardNumber, Iban? iban)
    {
        CardId = cardId;
        CardNumber = cardNumber;
        Iban = iban;
    }

    public Guid CardId { get; private set; }
    public CardNumber? CardNumber { get; private set; }
    public Iban? Iban { get; private set; }

    internal void Change(CardNumber? cardNumber, Iban? iban)
    {
        CardNumber = cardNumber;
        Iban = iban;
    }
}
