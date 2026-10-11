using System.Globalization;
using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class CultureSelectionTests
{
    private static readonly CultureInfo SystemUiCulture = CultureInfo.GetCultureInfo("fr-FR");

    [TestCase(CultureSelection.EnglishTag)]
    [TestCase(CultureSelection.SpanishTag)]
    public void ASupportedLanguageIsUsed(string languageTag)
    {
        CultureInfo culture = CultureSelection.ResolveUiCulture(languageTag, SystemUiCulture);

        Assert.That(culture.Name, Is.EqualTo(languageTag));
    }

    [Test]
    public void TheTagIsMatchedWithoutRegardToCase()
    {
        CultureInfo culture = CultureSelection.ResolveUiCulture("ES-es", SystemUiCulture);

        Assert.That(culture.Name, Is.EqualTo(CultureSelection.SpanishTag));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("tlh-KX")]
    [TestCase("de-DE")]
    [TestCase("not a culture")]
    public void AnythingElseFallsBackToTheSystemLanguage(string? languageTag)
    {
        CultureInfo culture = CultureSelection.ResolveUiCulture(languageTag, SystemUiCulture);

        Assert.That(culture, Is.SameAs(SystemUiCulture));
    }

    [Test]
    public void EverySupportedTagIsARealCulture()
    {
        Assert.That(
            CultureSelection.SupportedLanguageTags.Select(tag => CultureInfo.GetCultureInfo(tag).Name),
            Is.EqualTo(CultureSelection.SupportedLanguageTags).IgnoreCase);
    }
}
