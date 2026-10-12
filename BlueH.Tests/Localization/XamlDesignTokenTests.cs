using System.IO;
using System.Xml.Linq;

namespace BlueH.Tests.Localization;

[TestFixture]
public class XamlDesignTokenTests
{
    private const char MarkupExtensionStart = '{';
    private const string HitTestableTransparent = "Transparent";

    private static readonly HashSet<string> TypographyAttributes = new(StringComparer.Ordinal)
    {
        "FontSize",
        "FontWeight",
        "FontFamily",
        "Opacity"
    };

    private static readonly HashSet<string> ColourAttributes = new(StringComparer.Ordinal)
    {
        "Foreground",
        "Background",
        "BorderBrush",
        "Fill",
        "Stroke"
    };

    private static readonly string[] ScannedDirectories = ["Views"];
    private static readonly string[] ScannedRootFiles = ["MainWindow.xaml"];

    [TestCaseSource(nameof(ViewFiles))]
    public void TypographyComesFromTheThemeRamp(string relativePath)
    {
        Assert.That(LiteralAttributes(relativePath, TypographyAttributes, allowed: null), Is.Empty);
    }

    [TestCaseSource(nameof(ViewFiles))]
    public void ColoursComeFromTheTheme(string relativePath)
    {
        Assert.That(LiteralAttributes(relativePath, ColourAttributes, HitTestableTransparent), Is.Empty);
    }

    [Test]
    public void TheScanCoversTheViews()
    {
        Assert.That(ViewFiles(), Has.Some.Contains("DevicesView.xaml").And.Some.EqualTo("MainWindow.xaml"));
    }

    private static string[] LiteralAttributes(string relativePath, HashSet<string> names, string? allowed)
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryFiles.ApplicationDirectory(), relativePath));

        return
        [
            .. document.Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => names.Contains(attribute.Name.LocalName))
                .Where(attribute => attribute.Value.Length > 0 && attribute.Value[0] != MarkupExtensionStart)
                .Where(attribute => !string.Equals(attribute.Value, allowed, StringComparison.Ordinal))
                .Select(attribute => $"{attribute.Parent!.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\"")
        ];
    }

    private static IEnumerable<string> ViewFiles()
    {
        string root = RepositoryFiles.ApplicationDirectory();

        return ScannedRootFiles
            .Concat(ScannedDirectories.SelectMany(directory => Directory
                .EnumerateFiles(Path.Combine(root, directory), "*.xaml")
                .Select(path => Path.GetRelativePath(root, path))))
            .Order(StringComparer.Ordinal);
    }
}
