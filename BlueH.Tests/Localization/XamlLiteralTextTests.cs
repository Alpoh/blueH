using System.IO;
using System.Xml.Linq;

namespace BlueH.Tests.Localization;

[TestFixture]
public class XamlLiteralTextTests
{
    private const char MarkupExtensionStart = '{';

    private static readonly HashSet<string> UserFacingAttributes = new(StringComparer.Ordinal)
    {
        "Text",
        "Content",
        "Header",
        "ToolTip",
        "Title",
        "PlaceholderText"
    };

    private static readonly HashSet<string> LayoutValueElements = new(StringComparer.Ordinal)
    {
        "Double",
        "Int32",
        "Thickness",
        "CornerRadius",
        "GridLength"
    };

    private static readonly string[] IgnoredDirectories = ["bin", "obj"];

    [TestCaseSource(nameof(XamlFiles))]
    public void NoUserFacingTextIsWrittenLiterallyInXaml(string relativePath)
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryFiles.ApplicationDirectory(), relativePath));

        string[] literals =
        [
            .. document.Descendants().SelectMany(LiteralsIn)
        ];

        Assert.That(literals, Is.Empty);
    }

    [Test]
    public void ALiteralStringResourceIsStillCaught()
    {
        XElement literal = XElement.Parse(
            "<String xmlns=\"clr-namespace:System;assembly=System.Runtime\">Connected</String>");

        Assert.That(LiteralsIn(literal), Is.Not.Empty);
    }

    [Test]
    public void TheScanFindsTheViews()
    {
        Assert.That(XamlFiles(), Has.Some.EndsWith("MainWindow.xaml").And.Some.Contains("Views"));
    }

    private static IEnumerable<string> LiteralsIn(XElement element)
    {
        foreach (XAttribute attribute in element.Attributes())
        {
            if (UserFacingAttributes.Contains(attribute.Name.LocalName)
                && attribute.Value.Length > 0
                && attribute.Value[0] != MarkupExtensionStart)
            {
                yield return $"{element.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\"";
            }
        }

        if (LayoutValueElements.Contains(element.Name.LocalName))
        {
            yield break;
        }

        foreach (XText text in element.Nodes().OfType<XText>())
        {
            if (!string.IsNullOrWhiteSpace(text.Value))
            {
                yield return $"{element.Name.LocalName}: \"{text.Value.Trim()}\"";
            }
        }
    }

    private static IEnumerable<string> XamlFiles()
    {
        string root = RepositoryFiles.ApplicationDirectory();

        return Directory
            .EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path => !IgnoredDirectories.Any(ignored =>
                path.StartsWith(ignored + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal);
    }
}
