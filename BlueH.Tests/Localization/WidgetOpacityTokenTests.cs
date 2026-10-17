using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace BlueH.Tests.Localization;

[TestFixture]
public class WidgetOpacityTokenTests
{
    private const string LayoutFile = "Themes/Layout.xaml";
    private const double LowestRestingOpacity = 0.8;
    private const double HighestRestingOpacity = 0.9;

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheWidgetRestsBetweenEightyAndNinetyPercentOpaque()
    {
        Assert.That(Token("WidgetRestingOpacity"), Is.InRange(LowestRestingOpacity, HighestRestingOpacity));
    }

    [Test]
    public void TheWidgetBecomesFullyOpaqueWhenActive()
    {
        Assert.That(Token("WidgetActiveOpacity"), Is.EqualTo(1));
    }

    [TestCase("IsMouseOver")]
    [TestCase("IsKeyboardFocusWithin")]
    public void TheWidgetTurnsSolidOn(string property)
    {
        XElement? trigger = WidgetWindowStyle()
            .Descendants(Presentation + "Trigger")
            .SingleOrDefault(element => (string?)element.Attribute("Property") == property);

        Assert.That(
            trigger?.Descendants(Presentation + "Setter").Select(setter => (string?)setter.Attribute("Value")),
            Is.EqualTo(new[] { "{StaticResource WidgetActiveOpacity}" }));
    }

    [Test]
    public void TheWidgetsTextUsesThePrimaryThemeColour()
    {
        XElement? foreground = WidgetWindowStyle()
            .Elements(Presentation + "Setter")
            .SingleOrDefault(setter => (string?)setter.Attribute("Property") == "Foreground");

        Assert.That((string?)foreground?.Attribute("Value"), Is.EqualTo("{DynamicResource TextFillColorPrimaryBrush}"));
    }

    private static XElement WidgetWindowStyle()
    {
        return Layout()
            .Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(Xaml + "Key") == "WidgetWindowStyle");
    }

    private static double Token(string key)
    {
        XElement token = Layout()
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Key") == key);

        return double.Parse(token.Value, CultureInfo.InvariantCulture);
    }

    private static XDocument Layout()
    {
        return XDocument.Load(Path.Combine(RepositoryFiles.ApplicationDirectory(), LayoutFile));
    }
}
