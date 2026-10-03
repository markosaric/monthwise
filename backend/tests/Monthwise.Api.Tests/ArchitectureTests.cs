using System.Reflection;

namespace Monthwise.Api.Tests;

/// <summary>Enforces the dependency direction Domain ← Application ← Infrastructure ← Api.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Domain.Common.Money).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library()
    {
        var references = ReferencedAssemblyNames(Domain);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard",
            $"Domain must not reference '{name}'."));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_frameworks()
    {
        var references = ReferencedAssemblyNames(Application);

        Assert.DoesNotContain("Monthwise.Infrastructure", references);
        Assert.DoesNotContain("Monthwise.Api", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var references = ReferencedAssemblyNames(Infrastructure);

        Assert.DoesNotContain("Monthwise.Api", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
}
