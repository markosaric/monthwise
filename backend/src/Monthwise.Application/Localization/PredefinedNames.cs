using System.Globalization;

namespace Monthwise.Application.Localization;

/// <summary>
/// The predefined input, expense and loan categories and loan subcategories in Croatian
/// (croatian-language.md FR-6). Names are stored once, in English; a predefined item is shown in
/// Croatian while its stored name is still its English name. Once the user renames it, the name
/// is the user's own text and is shown as typed. Banks and card issuers are names, never translated.
/// </summary>
public static class PredefinedNames
{
    private static readonly Dictionary<Guid, (string English, string Croatian)> Names = new()
    {
        // Input categories (input cash flow FR-6).
        [new Guid("0199b0c0-0000-7000-8000-000000000201")] = ("Salary", "Plaća"),
        [new Guid("0199b0c0-0000-7000-8000-000000000202")] = ("Salary add-ons", "Dodaci na plaću"),
        [new Guid("0199b0c0-0000-7000-8000-000000000203")] = ("Agreed contributions", "Dogovoreni doprinosi"),
        [new Guid("0199b0c0-0000-7000-8000-000000000204")] = ("Other income", "Ostali prihodi"),

        // Expense categories (bills and expenses FR-1, FR-8).
        [new Guid("0199c8b0-0000-7000-8000-000000000401")] = ("Electricity", "Struja"),
        [new Guid("0199c8b0-0000-7000-8000-000000000402")] = ("Gas", "Plin"),
        [new Guid("0199c8b0-0000-7000-8000-000000000403")] = ("Water and waste", "Voda i odvoz otpada"),
        [new Guid("0199c8b0-0000-7000-8000-000000000404")] = ("Building reserve", "Pričuva"),
        [new Guid("0199c8b0-0000-7000-8000-000000000405")] = ("Internet", "Internet"),
        [new Guid("0199c8b0-0000-7000-8000-000000000406")] = ("Television", "Televizija"),
        [new Guid("0199c8b0-0000-7000-8000-000000000407")] = ("Telephone", "Telefon"),
        [new Guid("0199c8b0-0000-7000-8000-000000000408")] = ("Mobile phone", "Mobitel"),
        [new Guid("0199c8b0-0000-7000-8000-000000000409")] = ("Gym membership", "Teretana"),
        [new Guid("0199c8b0-0000-7000-8000-000000000410")] = ("Other bills", "Ostali računi"),
        [new Guid("0199c8b0-0000-7000-8000-000000000421")] = ("Groceries", "Namirnice"),
        [new Guid("0199c8b0-0000-7000-8000-000000000422")] = ("Personal care", "Osobna njega"),
        [new Guid("0199c8b0-0000-7000-8000-000000000423")] = ("Kindergarten", "Vrtić"),
        [new Guid("0199c8b0-0000-7000-8000-000000000424")] = ("Household", "Kućanstvo"),
        [new Guid("0199c8b0-0000-7000-8000-000000000425")] = ("Transport", "Prijevoz"),
        [new Guid("0199c8b0-0000-7000-8000-000000000426")] = ("Other expenses", "Ostali troškovi"),
        [new Guid("0199c8b0-0000-7000-8000-000000000499")] = ("Cash withdrawal", "Podizanje gotovine"),

        // Loan categories and subcategories (loans and leases FR-8).
        [new Guid("0199c2a0-0000-7000-8000-000000000301")] = ("Bank loan", "Bankovni kredit"),
        [new Guid("0199c2a0-0000-7000-8000-000000000302")] = ("Lease", "Leasing"),
        [new Guid("0199c2a0-0000-7000-8000-000000000311")] = ("Mortgage", "Stambeni kredit"),
        [new Guid("0199c2a0-0000-7000-8000-000000000312")] = ("Cash loan", "Gotovinski kredit"),
        [new Guid("0199c2a0-0000-7000-8000-000000000313")] = ("Car loan", "Kredit za automobil"),
        [new Guid("0199c2a0-0000-7000-8000-000000000314")] = ("Other loan", "Ostali krediti"),
        [new Guid("0199c2a0-0000-7000-8000-000000000321")] = ("Car lease", "Leasing automobila"),
        [new Guid("0199c2a0-0000-7000-8000-000000000322")] = ("Equipment lease", "Leasing opreme"),
        [new Guid("0199c2a0-0000-7000-8000-000000000323")] = ("Other lease", "Ostali leasing"),
    };

    /// <summary>The ids of all predefined items, for tests that compare them with the seed data.</summary>
    public static IReadOnlyCollection<Guid> Ids => Names.Keys;

    /// <summary>The English and Croatian name of a predefined item, or null for the user's own items.</summary>
    public static (string English, string Croatian)? Of(Guid id) => Names.TryGetValue(id, out var names) ? names : null;

    /// <summary>The name to show in the language of the request.</summary>
    public static string Display(Guid id, string storedName)
    {
        ArgumentNullException.ThrowIfNull(storedName);
        return AppLanguage.IsCroatian && Translation(id, storedName) is { } croatian ? croatian : storedName;
    }

    /// <summary>
    /// Whether a new or changed name would look like another item's predefined name in Croatian
    /// (croatian-language.md acceptance criterion 6). Names compare as the database compares them:
    /// letter case ignored, accents kept.
    /// </summary>
    public static bool ClashesWithTranslation(string name, Guid? exceptId, IEnumerable<(Guid Id, string Name)> existing)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existing);
        return existing.Any(item => item.Id != exceptId
            && Translation(item.Id, item.Name) is { } croatian
            && CultureInfo.InvariantCulture.CompareInfo.Compare(croatian, name.Trim(), CompareOptions.IgnoreCase) == 0);
    }

    /// <summary>The Croatian name while the stored name is still the predefined English one.</summary>
    private static string? Translation(Guid id, string storedName) =>
        Names.TryGetValue(id, out var names) && string.Equals(names.English, storedName, StringComparison.Ordinal)
            ? names.Croatian
            : null;
}
