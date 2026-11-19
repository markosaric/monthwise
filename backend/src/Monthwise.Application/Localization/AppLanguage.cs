using System.Globalization;

namespace Monthwise.Application.Localization;

/// <summary>
/// The language of the current request (croatian-language.md FR-3): Croatian when the request's
/// UI culture is Croatian, English otherwise. The API sets only the UI culture from the request,
/// never the culture used to read or format amounts and dates (FR-5, FR-7).
/// </summary>
public static class AppLanguage
{
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    public static readonly CultureInfo Croatian = CultureInfo.GetCultureInfo("hr");

    public static bool IsCroatian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "hr";
}
