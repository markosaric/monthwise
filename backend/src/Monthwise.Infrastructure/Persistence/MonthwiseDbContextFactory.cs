using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure.Persistence;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools at design time, so migrations can be created without
/// starting the API. Reads <c>ConnectionStrings__Monthwise</c> or falls back to a local default.
/// </summary>
internal sealed class MonthwiseDbContextFactory : IDesignTimeDbContextFactory<MonthwiseDbContext>
{
    private const string LocalDefault = "Host=localhost;Port=5432;Database=monthwise;Username=monthwise";

    public MonthwiseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Monthwise") ?? LocalDefault;
        var options = new DbContextOptionsBuilder<MonthwiseDbContext>();
        DependencyInjection.ConfigureDbContext(options, connectionString);
        return new MonthwiseDbContext(options.Options, new DesignTimeCipher());
    }

    /// <summary>Migrations never read or write card numbers, so no keys are needed at design time.</summary>
    private sealed class DesignTimeCipher : ISensitiveDataCipher
    {
        public string Encrypt(string plaintext) => throw new NotSupportedException();
        public string Decrypt(string stored) => throw new NotSupportedException();
        public string Hash(string value) => throw new NotSupportedException();
    }
}
