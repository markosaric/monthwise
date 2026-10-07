namespace Monthwise.Api.Tests;

/// <summary>Test classes that share the test database run one after another, never in parallel.</summary>
[CollectionDefinition(Name)]
public sealed class SharedDatabase
{
    public const string Name = "Database";
}
