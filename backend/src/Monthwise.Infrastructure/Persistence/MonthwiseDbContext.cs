using Microsoft.EntityFrameworkCore;
using Monthwise.Domain.Common;
using Monthwise.Infrastructure.Persistence.Converters;

namespace Monthwise.Infrastructure.Persistence;

public sealed class MonthwiseDbContext(DbContextOptions<MonthwiseDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MonthwiseDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>();
        configurationBuilder.Properties<YearMonth>().HaveConversion<YearMonthConverter>();
    }
}
