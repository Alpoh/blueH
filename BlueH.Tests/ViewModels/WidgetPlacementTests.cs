using System.Windows;
using BlueH.Models;
using BlueH.ViewModels;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class WidgetPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly WidgetPosition Fallback = new(1500, 24);

    [Test]
    public void WithNothingRememberedTheFallbackIsUsed()
    {
        Assert.That(WidgetPlacement.Place(remembered: null, Fallback, Screen), Is.EqualTo(Fallback));
    }

    [Test]
    public void ARememberedPositionOnScreenIsKept()
    {
        WidgetPosition remembered = new(640, 480);

        Assert.That(WidgetPlacement.Place(remembered, Fallback, Screen), Is.EqualTo(remembered));
    }

    [Test]
    public void APositionLeftOfTheScreenIsPulledBack()
    {
        Assert.That(WidgetPlacement.Place(new WidgetPosition(-900, 300), Fallback, Screen), Is.EqualTo(new WidgetPosition(0, 300)));
    }

    [Test]
    public void APositionOnAMonitorThatIsGoneKeepsAGripOnScreen()
    {
        WidgetPosition placed = WidgetPlacement.Place(new WidgetPosition(3500, 2000), Fallback, Screen);

        Assert.That(
            placed,
            Is.EqualTo(new WidgetPosition(Screen.Right - WidgetPlacement.VisibleGrip, Screen.Bottom - WidgetPlacement.VisibleGrip)));
    }

    [Test]
    public void AScreenThatStartsLeftOfThePrimaryMonitorIsRespected()
    {
        Rect twoMonitors = new(-1920, 0, 3840, 1080);
        WidgetPosition onLeftMonitor = new(-1000, 200);

        Assert.That(WidgetPlacement.Place(onLeftMonitor, Fallback, twoMonitors), Is.EqualTo(onLeftMonitor));
    }

    [Test]
    public void ANonFinitePositionFallsBack()
    {
        Assert.That(WidgetPlacement.Place(new WidgetPosition(double.NaN, 10), Fallback, Screen), Is.EqualTo(Fallback));
    }
}
