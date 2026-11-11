using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monthwise.Infrastructure.Persistence;

/// <summary>
/// The one meaning of "unique, ignoring upper/lower case" for names (name comparison FR-1): a
/// case-insensitive, accent-sensitive ICU collation on every name column of a name list. The
/// database applies it to the unique indexes and to every comparison of those columns, whatever
/// locale the database itself was created with.
/// </summary>
internal static class NameCollation
{
    public const string Name = "monthwise_name";

    /// <summary>ICU root locale at strength 2: letters and accents count, upper/lower case does not.</summary>
    public const string Locale = "und-u-ks-level2";

    public const string Provider = "icu";

    public static ModelBuilder HasNameCollation(this ModelBuilder modelBuilder) =>
        modelBuilder.HasCollation(Name, locale: Locale, provider: Provider, deterministic: false);

    public static PropertyBuilder<string> UseNameCollation(this PropertyBuilder<string> property) =>
        property.UseCollation(Name);

    /// <summary>
    /// The same rule in memory, for names not in the database yet (FR-4) and for checking a backup
    /// before it is restored (FR-7): ICU root comparison ignoring case only, as <see cref="Locale"/>.
    /// </summary>
    public static bool AreSame(string a, string b) =>
        CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase) == 0;
}
