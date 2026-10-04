using FluentValidation;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Application.Cards;

/// <summary>A validated card request: parsed details and the final numbers to store.</summary>
public sealed record CardInput(CardDetails Details, CardNumber? CardNumber, Iban? Iban);

/// <summary>Checks a create or edit request against formats, the lists, and uniqueness.</summary>
public sealed class CardInputChecker(
    IValidator<CardRequest> validator,
    ICardRepository cards,
    ICardListRepository<CardIssuer> issuers,
    ICardListRepository<Bank> banks)
{
    /// <param name="existing">The card being edited (loaded with its sensitive data), or null when adding.</param>
    public async Task<Result<CardInput>> CheckAsync(CardRequest request, Card? existing, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CardInput>(validation.ToDomainErrors());
        }

        var details = CardMapping.ToDetails(request);
        var cardNumber = Resolve(request.CardNumber, existing?.SensitiveData.CardNumber, CardNumber.Parse);
        var iban = Resolve(request.Iban, existing?.SensitiveData.Iban, Iban.Parse);

        var errors = new List<DomainError>();
        if (details.IssuerId != Guid.Empty && !await issuers.ExistsAsync(details.IssuerId, cancellationToken))
        {
            errors.Add(CardErrors.IssuerUnknown);
        }
        if (details.BankId != Guid.Empty && !await banks.ExistsAsync(details.BankId, cancellationToken))
        {
            errors.Add(CardErrors.BankUnknown);
        }
        if (!string.IsNullOrWhiteSpace(details.Name)
            && await cards.NameInUseAsync(details.Name.Trim(), existing?.Id, cancellationToken))
        {
            errors.Add(CardErrors.NameInUse);
        }
        if (cardNumber is not null && await cards.CardNumberInUseAsync(cardNumber, existing?.Id, cancellationToken))
        {
            errors.Add(CardErrors.CardNumberInUse);
        }
        if (iban is not null && await cards.IbanInUseAsync(iban, existing?.Id, cancellationToken))
        {
            errors.Add(CardErrors.IbanInUse);
        }

        return errors.Count > 0
            ? Result.Failure<CardInput>(errors)
            : Result.Success(new CardInput(details, cardNumber, iban));
    }

    /// <summary>null keeps the stored value, an empty string removes it, anything else replaces it.</summary>
    private static T? Resolve<T>(string? requested, T? stored, Func<string, T> parse) where T : class =>
        requested switch
        {
            null => stored,
            "" => null,
            _ => parse(requested),
        };
}
