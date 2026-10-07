using Monthwise.Domain.Cards;

namespace Monthwise.Application.Cards;

/// <summary>
/// Add or edit a card. Months use the ISO form <c>yyyy-MM</c>. On edit, a <c>null</c>
/// <see cref="CardNumber"/> or <see cref="Iban"/> keeps the stored value and an empty string removes it.
/// </summary>
public sealed record CardRequest(
    string Name,
    Guid IssuerId,
    Guid BankId,
    string? CardNumber,
    string? Iban,
    string? ValidFrom,
    string ValidThrough,
    CardType Type,
    bool IsPrimary,
    long? DebtLimitCents,
    bool AllowsInstallments);

public sealed record CardStateRequest(string? Reason);

public sealed record ExpireCardRequest(string? Reason, Guid? ReplacementCardId);

public sealed record CardListEntryRequest(string Name);

public sealed record CardListEntryResponse(Guid Id, string Name, bool IsPredefined);

public sealed record CardReference(Guid Id, string Name);

/// <summary>A card as the UI shows it. Never contains a full card number or IBAN.</summary>
public sealed record CardResponse(
    Guid Id,
    string Name,
    CardListEntryResponse Issuer,
    CardListEntryResponse Bank,
    string? CardNumberMasked,
    string? IbanMasked,
    string? ValidFrom,
    string ValidThrough,
    CardType Type,
    bool IsPrimary,
    long? DebtLimitCents,
    bool AllowsInstallments,
    CardStatus Status,
    bool CanBeUsed,
    DateOnly? BlockedOn,
    string? BlockReason,
    DateOnly? ExpiredOn,
    string? ExpiryReason,
    DateOnly? ArchivedOn,
    CardReference? ReplacedBy,
    CardReference? Replaces);

/// <summary>
/// The Dashboard widget: the cards shown, whether any card exists, and the balance of each shown
/// card (input cash flow FR-11).
/// </summary>
public sealed record DashboardCardsResponse(
    bool HasAnyCards,
    IReadOnlyList<CardResponse> Cards,
    IReadOnlyList<CashFlow.AccountBalanceResponse> Balances);

/// <summary>One full number, formatted in groups of four, sent only when the user presses Reveal.</summary>
public sealed record RevealedNumberResponse(string Value);

public enum CardListFilter
{
    /// <summary>Everything except archived cards and expired cards without ongoing transactions.</summary>
    Default,
    Expired,
    Archived,
}

public enum SensitiveNumberKind
{
    CardNumber,
    Iban,
}
