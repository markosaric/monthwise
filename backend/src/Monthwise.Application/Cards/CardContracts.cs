using Monthwise.Domain.Cards;

namespace Monthwise.Application.Cards;

/// <summary>
/// Add or edit a card. Months use the ISO form <c>yyyy-MM</c>. On edit, a <c>null</c>
/// <see cref="CardNumber"/> or <see cref="Iban"/> keeps the stored value and an empty string removes it.
/// The billing details (<see cref="StatementDay"/> to <see cref="MaxInstallments"/>) are required for
/// a card that allows monthly installments and ignored for other cards (installments FR-1).
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
    bool AllowsInstallments,
    int? StatementDay = null,
    int? PaymentDay = null,
    Guid? PayingAccountId = null,
    bool PaysFromAccountDirectly = false,
    int? MaxInstallments = null);

public sealed record CardStateRequest(string? Reason);

public sealed record ExpireCardRequest(string? Reason, Guid? ReplacementCardId);

public sealed record CardListEntryRequest(string Name);

public sealed record CardListEntryResponse(Guid Id, string Name, bool IsPredefined);

public sealed record CardReference(Guid Id, string Name);

/// <summary>
/// One version of a card's statement and payment day. <see cref="FromMonth"/> is null for the first
/// version, which covers every earlier month (installments FR-2).
/// </summary>
public sealed record BillingTermsResponse(string? FromMonth, int StatementDay, int PaymentDay);

/// <summary>
/// The billing details of a card that allows installments (installments FR-1). The days are the ones
/// from next month on, as the card form edits them; <see cref="Terms"/> lists every version.
/// </summary>
public sealed record CardBillingResponse(
    int StatementDay,
    int PaymentDay,
    CardReference PayingAccount,
    bool PaysFromAccountDirectly,
    int MaxInstallments,
    IReadOnlyList<BillingTermsResponse> Terms);

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
    CardReference? Replaces,
    CardBillingResponse? Billing,
    bool NeedsBillingDetails);

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
