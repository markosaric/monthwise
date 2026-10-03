using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Monthwise.Domain.Common;

namespace Monthwise.Infrastructure.Persistence.Converters;

/// <summary>Stores <see cref="YearMonth"/> as a <c>date</c> on the first day of the month.</summary>
internal sealed class YearMonthConverter() : ValueConverter<YearMonth, DateOnly>(
    value => value.FirstDay,
    date => YearMonth.FromDate(date));
