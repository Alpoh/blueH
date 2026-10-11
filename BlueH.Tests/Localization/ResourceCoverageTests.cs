using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BlueH.Tests.Localization;

[TestFixture]
public partial class ResourceCoverageTests
{
    private const string NeutralFileName = "Resources.resx";
    private const string DesignerFileName = "Resources.Designer.cs";
    private const string CultureFilePattern = "Resources.*.resx";

    private static readonly string[] RequiredCultures = ["es", "qps-ploc"];

    [Test]
    public void EveryNeutralKeyHasAGeneratedProperty()
    {
        string designer = File.ReadAllText(Path.Combine(RepositoryFiles.PropertiesDirectory(), DesignerFileName));
        HashSet<string> properties = [.. DesignerPropertyPattern().Matches(designer).Select(match => match.Groups[1].Value)];

        Assert.That(properties, Is.EquivalentTo(Neutral().Keys));
    }

    [Test]
    public void TheRequiredCulturesExist()
    {
        Assert.That(CultureFiles().Select(CultureOf), Is.SupersetOf(RequiredCultures));
    }

    [TestCaseSource(nameof(CultureFiles))]
    public void EveryCultureHasExactlyTheNeutralKeys(string cultureFile)
    {
        Dictionary<string, string> culture = Read(cultureFile);
        Dictionary<string, string> neutral = Neutral();

        Assert.Multiple(() =>
        {
            Assert.That(neutral.Keys.Except(culture.Keys), Is.Empty, "missing from the culture");
            Assert.That(culture.Keys.Except(neutral.Keys), Is.Empty, "not in the neutral file");
        });
    }

    [TestCaseSource(nameof(CultureFiles))]
    public void EveryTranslationKeepsTheFormatPlaceholders(string cultureFile)
    {
        Dictionary<string, string> culture = Read(cultureFile);

        string[] mismatched =
        [
            .. Neutral()
                .Where(entry => culture.ContainsKey(entry.Key))
                .Where(entry => !Placeholders(entry.Value).SetEquals(Placeholders(culture[entry.Key])))
                .Select(entry => entry.Key)
        ];

        Assert.That(mismatched, Is.Empty);
    }

    [TestCaseSource(nameof(CultureFiles))]
    public void NoTranslationIsBlank(string cultureFile)
    {
        string[] blank = [.. Read(cultureFile).Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => entry.Key)];

        Assert.That(blank, Is.Empty);
    }

    private static IEnumerable<string> CultureFiles()
    {
        return Directory
            .EnumerateFiles(RepositoryFiles.PropertiesDirectory(), CultureFilePattern)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal);
    }

    private static string CultureOf(string cultureFile)
    {
        return cultureFile["Resources.".Length..^".resx".Length];
    }

    private static Dictionary<string, string> Neutral()
    {
        return Read(NeutralFileName);
    }

    private static Dictionary<string, string> Read(string fileName)
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryFiles.PropertiesDirectory(), fileName));

        return document.Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static HashSet<string> Placeholders(string value)
    {
        return [.. PlaceholderPattern().Matches(value).Select(match => match.Value)];
    }

    [GeneratedRegex(@"internal static string (\w+) \{")]
    private static partial Regex DesignerPropertyPattern();

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
