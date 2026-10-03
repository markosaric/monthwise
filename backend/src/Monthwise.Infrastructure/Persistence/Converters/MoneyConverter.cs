using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Monthwise.Domain.Common;

namespace Monthwise.Infrastructure.Persistence.Converters;

/// <summary>Stores <see cref="Money"/> as a <c>bigint</c> number of cents.</summary>
internal sealed class MoneyConverter() : ValueConverter<Money, long>(
    money => money.Cents,
    cents => Money.FromCents(cents));
