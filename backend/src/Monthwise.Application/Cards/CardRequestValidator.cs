using FluentValidation;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Application.Cards;

/// <summary>
/// Checks the shape of a card request (formats that must parse). The card rules themselves live in
/// <see cref="Card"/>. Messages never repeat the entered number.
/// </summary>
internal sealed class CardRequestValidator : AbstractValidator<CardRequest>
{
    public CardRequestValidator()
    {
        RuleFor(r => r.ValidFrom)
            .Must(BeAYearMonth).When(r => !string.IsNullOrEmpty(r.ValidFrom)).WithMessage("Enter the month and year as printed on the card.")
            .OverridePropertyName("validFrom");
        RuleFor(r => r.ValidThrough)
            .Must(BeAYearMonth).WithMessage("Enter the month and year as printed on the card.")
            .OverridePropertyName("validThrough");
        RuleFor(r => r.CardNumber)
            .Must(n => CardNumber.TryParse(n, out _)).When(r => !string.IsNullOrEmpty(r.CardNumber))
            .WithErrorCode(CardErrors.CardNumberInvalid.Code).WithMessage(CardErrors.CardNumberInvalid.Message)
            .OverridePropertyName("cardNumber");
        RuleFor(r => r.Iban)
            .Must(i => Iban.TryParse(i, out _)).When(r => !string.IsNullOrEmpty(r.Iban))
            .WithErrorCode(CardErrors.IbanInvalid.Code).WithMessage(CardErrors.IbanInvalid.Message)
            .OverridePropertyName("iban");
        RuleFor(r => r.Type).IsInEnum().OverridePropertyName("type");
    }

    private static bool BeAYearMonth(string? value) => YearMonth.TryParse(value, out _);
}

internal static class ValidationResultExtensions
{
    public static IEnumerable<DomainError> ToDomainErrors(this FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(e => new DomainError(e.ErrorCode, e.ErrorMessage, e.PropertyName));
}
