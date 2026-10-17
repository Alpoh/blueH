using BlueH.ViewModels;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class DisplayModeViewModelTests
{
    [Test]
    public void BlueHStartsInTheFullWindow()
    {
        DisplayModeViewModel displayMode = new();

        Assert.That(displayMode.IsWidgetMode, Is.False);
    }

    [Test]
    public void SwitchingToTheWidgetIsAnnounced()
    {
        DisplayModeViewModel displayMode = new();
        List<string?> changed = [];
        displayMode.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        displayMode.SwitchToWidgetCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(displayMode.IsWidgetMode, Is.True);
            Assert.That(changed, Does.Contain(nameof(DisplayModeViewModel.IsWidgetMode)));
        });
    }

    [Test]
    public void TheWidgetSwitchesBackToTheFullWindow()
    {
        DisplayModeViewModel displayMode = new();
        displayMode.SwitchToWidgetCommand.Execute(null);

        displayMode.SwitchToWindowCommand.Execute(null);

        Assert.That(displayMode.IsWidgetMode, Is.False);
    }

    [Test]
    public void EachSwitchIsOnlyOfferedFromTheOtherMode()
    {
        DisplayModeViewModel displayMode = new();
        bool widgetOfferedInWindow = displayMode.SwitchToWidgetCommand.CanExecute(null);
        bool windowOfferedInWindow = displayMode.SwitchToWindowCommand.CanExecute(null);

        displayMode.SwitchToWidgetCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(widgetOfferedInWindow, Is.True);
            Assert.That(windowOfferedInWindow, Is.False);
            Assert.That(displayMode.SwitchToWidgetCommand.CanExecute(null), Is.False);
            Assert.That(displayMode.SwitchToWindowCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void ExitingAsksTheApplicationToClose()
    {
        DisplayModeViewModel displayMode = new();
        int requests = 0;
        displayMode.ExitRequested += (_, _) => requests++;

        displayMode.ExitCommand.Execute(null);

        Assert.That(requests, Is.EqualTo(1));
    }

    [Test]
    public void EveryControlHasAName()
    {
        DisplayModeViewModel displayMode = new();

        Assert.Multiple(() =>
        {
            Assert.That(displayMode.SwitchToWidgetLabel, Is.Not.Empty);
            Assert.That(displayMode.SwitchToWindowLabel, Is.Not.Empty);
            Assert.That(displayMode.ExitLabel, Is.Not.Empty);
        });
    }
}
