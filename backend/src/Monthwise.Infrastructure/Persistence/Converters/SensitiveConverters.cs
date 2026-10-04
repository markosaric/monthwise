using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Monthwise.Domain.Cards;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="CardNumber"/> encrypted; the database never sees the digits.</summary>
internal sealed class EncryptedCardNumberConverter(ISensitiveDataCipher cipher) : ValueConverter<CardNumber?, string>(
    number => cipher.Encrypt(number!.Digits),
    stored => CardNumber.Parse(cipher.Decrypt(stored)));

/// <summary>Stores an <see cref="Iban"/> encrypted.</summary>
internal sealed class EncryptedIbanConverter(ISensitiveDataCipher cipher) : ValueConverter<Iban?, string>(
    iban => cipher.Encrypt(iban!.Value),
    stored => Iban.Parse(cipher.Decrypt(stored)));
