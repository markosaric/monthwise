using Monthwise.Domain.Common;

namespace Monthwise.Domain.Cards;

/// <summary>The attributes the user enters for a card, apart from the sensitive numbers.</summary>
public sealed record CardDetails(
    string Name,
    Guid IssuerId,
    Guid BankId,
    YearMonth? ValidFrom,
    YearMonth ValidThrough,
    CardType Type,
    bool IsPrimary,
    Money? DebtLimit,
    bool AllowsInstallments = false,
    CardBillingDetails? Billing = null);
