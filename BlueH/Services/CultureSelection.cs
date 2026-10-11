using System.Globalization;

namespace BlueH.Services;

public static class CultureSelection
{
    public const string EnglishTag = "en-US";
    public const string SpanishTag = "es-ES";
    public const string PseudoLocaleTag = "qps-ploc";

    public static IReadOnlyList<string> SupportedLanguageTags { get; } =
#if DEBUG
        [EnglishTag, SpanishTag, PseudoLocaleTag];
#else
        [EnglishTag, SpanishTag];
#endif

    public static string? NormalizeSupportedTag(string? languageTag)
    {
        return SupportedLanguageTags.FirstOrDefault(supported =>
            string.Equals(supported, languageTag, StringComparison.OrdinalIgnoreCase));
    }

    public static CultureInfo ResolveUiCulture(string? languageTag, CultureInfo systemUiCulture)
    {
        return NormalizeSupportedTag(languageTag) is { } supported
            ? CultureInfo.GetCultureInfo(supported)
            : systemUiCulture;
    }

    public static string NativeDisplayName(string languageTag)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(languageTag);

        return culture.TextInfo.ToTitleCase(culture.NativeName);
    }
}
