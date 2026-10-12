using System.IO;
using System.Xml.Linq;

namespace BlueH.Tests.Localization;

[TestFixture]
public class XamlAccessibilityTests
{
    private const string AutomationNameAttribute = "AutomationProperties.Name";
    private const string GlyphMarker = "Glyph";

    private static readonly HashSet<string> ButtonElements = new(StringComparer.Ordinal)
    {
        "Button",
        "ToggleButton"
    };

    private static readonly string[] ScannedDirectories = ["Views"];
    private static readonly string[] ScannedRootFiles = ["MainWindow.xaml"];

    [TestCaseSource(nameof(ViewFiles))]
    public void EveryIconOnlyButtonHasAnAccessibleName(string relativePath)
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryFiles.ApplicationDirectory(), relativePath));

        string[] unnamed =
        [
            .. document.Descendants()
                .Where(element => ButtonElements.Contains(element.Name.LocalName))
                .Where(IsIconOnly)
                .Where(element => element.Attribute(AutomationNameAttribute) is null)
                .Select(element => (string?)element.Attribute("Content") ?? element.Name.LocalName)
        ];

        Assert.That(unnamed, Is.Empty);
    }

    [Test]
    public void AnUnnamedIconButtonIsCaught()
    {
        XElement button = XElement.Parse("<Button Content=\"{x:Static themes:Glyphs.Back}\" />");

        Assert.That(IsIconOnly(button) && button.Attribute(AutomationNameAttribute) is null, Is.True);
    }

    private static bool IsIconOnly(XElement element)
    {
        string? content = (string?)element.Attribute("Content");

        return content is not null && content.Contains(GlyphMarker, StringComparison.Ordinal);
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
