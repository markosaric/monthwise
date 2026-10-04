namespace Monthwise.Domain.Cards;

/// <summary>
/// The one status a card has on a given day. Statuses set by the user (Archived, Blocked, manual
/// expiry) take priority over the ones derived from the validity dates.
/// </summary>
public enum CardStatus
{
    Archived,
    Blocked,
    Expired,
    NotYetValid,
    ExpiringSoon,
    Active,
}
