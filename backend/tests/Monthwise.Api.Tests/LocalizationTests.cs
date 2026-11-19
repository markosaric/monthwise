using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Monthwise.Api.Endpoints;
using Monthwise.Api.Hosting;
using Monthwise.Application.Common;
using Monthwise.Application.Localization;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Infrastructure.Persistence.Configurations;

namespace Monthwise.Api.Tests;

/// <summary>
/// Every message and name the API makes up for the user has a Croatian text (croatian-language.md
/// FR-3, FR-6, FR-9, acceptance criterion 9). A message added in English only fails here.
/// </summary>
public sealed partial class LocalizationTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(Money).Assembly,
        typeof(Application.DependencyInjection).Assembly,
        typeof(Infrastructure.DependencyInjection).Assembly,
    ];

    [Fact]
    public void Every_domain_error_has_a_croatian_message()
    {
        var errors = Assemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(DomainError))
                .Select(f => (Name: $"{t.Name}.{f.Name}", Error: (DomainError)f.GetValue(null)!))
                .Concat(t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(p => p.PropertyType == typeof(DomainError) && p.GetIndexParameters().Length == 0)
                    .Select(p => (Name: $"{t.Name}.{p.Name}", Error: (DomainError)p.GetValue(null)!))))
            .ToList();

        Assert.True(errors.Count > 100, $"Only {errors.Count} errors found; the search is broken.");
        Assert.Empty(errors.Where(e => ServerTexts.ToCroatian(e.Error.Message) is null).Select(e => $"{e.Name}: {e.Error.Message}"));
    }

    [Fact]
    public void Every_message_written_in_the_source_has_a_croatian_text()
    {
        var messages = SourceFiles()
            .SelectMany(file => MessageLiteral().Matches(File.ReadAllText(file))
                .Select(m => (File: Path.GetFileName(file), Text: Regex.Unescape(m.Groups["text"].Value))))
            .ToList();

        Assert.True(messages.Count > 50, $"Only {messages.Count} messages found; the search is broken.");
        Assert.Empty(messages.Where(m => ServerTexts.ToCroatian(m.Text) is null).Select(m => $"{m.File}: {m.Text}"));
    }

    [Fact]
    public void Messages_with_values_and_names_the_api_makes_up_have_a_croatian_text()
    {
        string[] texts =
        [
            SupportedRange.MonthMessage,
            SupportedRange.DateMessage,
            MonthParameter.FormatMessage,
            "Choose a period of at most 10 years.",
            "Unknown",
            "No account yet",
            "Card payments",
            "Car lease, residual value",
            "An import is running. Try again in a moment.",
            "The database could not be brought up to date. See the log for details.",
            .. Enum.GetValues<DatabaseRule>().Select(DatabaseRuleException.MessageOf),
            .. NameLists().Select(list => Domain.DataTransfer.DataTransferErrors.SameNames(list, "Ana", "ANA").Message),
            Domain.DataTransfer.DataTransferErrors.NewerRelease("9.9.9").Message,
        ];

        Assert.DoesNotContain(texts, t => ServerTexts.ToCroatian(t) is null);
        Assert.Equal("Leasing automobila, ostatak vrijednosti", ServerTexts.ToCroatian("Leasing automobila, residual value"));
    }

    [Fact]
    public void Translate_keeps_english_unless_the_request_is_croatian()
    {
        InCulture(AppLanguage.English, () => Assert.Equal("Unknown", ServerTexts.Translate("Unknown")));
        InCulture(AppLanguage.Croatian, () => Assert.Equal("Nepoznato", ServerTexts.Translate("Unknown")));
        InCulture(AppLanguage.Croatian, () => Assert.Equal("A text without a translation", ServerTexts.Translate("A text without a translation")));
    }

    [Fact]
    public void Every_predefined_item_has_its_seed_name_in_english_and_a_croatian_name()
    {
        var seeds = CashFlowSeedData.Categories
            .Concat(ExpenseCategorySeedData.Starting)
            .Append((ExpenseCategorySeedData.CashWithdrawalId, ExpenseCategorySeedData.CashWithdrawalName))
            .Concat(LoanSeedData.Categories.Select(c => (c.Id, c.Name)))
            .Concat(LoanSeedData.Subcategories.Select(s => (s.Id, s.Name)))
            .ToList();

        Assert.Equal(seeds.Select(s => s.Item1).Order(), PredefinedNames.Ids.Order());
        Assert.All(seeds, seed =>
        {
            var names = PredefinedNames.Of(seed.Item1);
            Assert.NotNull(names);
            Assert.Equal(seed.Item2, names.Value.English);
            Assert.False(string.IsNullOrWhiteSpace(names.Value.Croatian));
        });
    }

    [Fact]
    public void A_predefined_name_is_shown_in_croatian_until_the_user_renames_it()
    {
        var salary = new Guid("0199b0c0-0000-7000-8000-000000000201");

        InCulture(AppLanguage.Croatian, () =>
        {
            Assert.Equal("Plaća", PredefinedNames.Display(salary, "Salary"));
            Assert.Equal("Primanja", PredefinedNames.Display(salary, "Primanja"));
            Assert.Equal("Salary", PredefinedNames.Display(Guid.NewGuid(), "Salary"));
        });
        InCulture(AppLanguage.English, () => Assert.Equal("Salary", PredefinedNames.Display(salary, "Salary")));
    }

    [Fact]
    public void A_name_like_another_items_croatian_name_clashes_ignoring_case_but_not_accents()
    {
        var salary = new Guid("0199b0c0-0000-7000-8000-000000000201");
        (Guid, string)[] existing = [(salary, "Salary")];

        Assert.True(PredefinedNames.ClashesWithTranslation("plaća", null, existing));
        Assert.True(PredefinedNames.ClashesWithTranslation("PLAĆA", null, existing));
        Assert.False(PredefinedNames.ClashesWithTranslation("Placa", null, existing));
        Assert.False(PredefinedNames.ClashesWithTranslation("Plaća", salary, existing));
        Assert.False(PredefinedNames.ClashesWithTranslation("Plaća", null, [(salary, "Primanja")]));
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("hr", "hr")]
    [InlineData("hr-HR", "hr")]
    [InlineData("HR", "hr")]
    [InlineData("hr-HR,hr;q=0.9,en;q=0.8", "hr")]
    [InlineData("en-US,hr;q=0.5", "en")]
    [InlineData("en;q=0.4,hr;q=0.9", "hr")]
    [InlineData("de-DE", "en")]
    [InlineData("not a language", "en")]
    public void The_request_language_follows_accept_language(string? header, string expected) =>
        Assert.Equal(expected, RequestLanguage.Of(header).TwoLetterISOLanguageName);

    private static void InCulture(CultureInfo culture, Action action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>How the import names each name list, read from its source so a new list is covered.</summary>
    private static IEnumerable<string> NameLists() =>
        NameList().Matches(File.ReadAllText(Path.Combine(SourceRoot(), "Monthwise.Infrastructure", "DataTransfer", "SameNameCheck.cs")))
            .Select(m => m.Groups[1].Value);

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}Localization{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var src = Path.Combine(dir.FullName, "src");
            if (File.Exists(Path.Combine(dir.FullName, "Monthwise.sln")) && Directory.Exists(src))
            {
                return src;
            }
        }
        throw new InvalidOperationException("The backend source folder was not found.");
    }

    /// <summary>
    /// A message written as text: the second text of <c>new DomainError("code", "message")</c> or
    /// <c>new("code", "message")</c> (codes have a dot), a <c>WithMessage("message")</c>, or a title of a problem.
    /// </summary>
    [GeneratedRegex(@"(?:new(?:\s+DomainError)?\(\s*""\w+(?:\.\w+)+"",\s*|WithMessage\(|title:\s*(?:ServerTexts\.Translate\()?)""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex MessageLiteral();

    [GeneratedRegex("""new\("[a-z_]+", "([a-z ]+)"[,)]""")]
    private static partial Regex NameList();
}
