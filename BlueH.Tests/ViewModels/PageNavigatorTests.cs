using BlueH.ViewModels.Pages;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class PageNavigatorTests
{
    private readonly TestPage _home = new("home");
    private readonly TestPage _section = new("section");
    private readonly TestPage _detail = new("detail");
    private readonly TestPage _deeper = new("deeper");

    [Test]
    public void ItStartsOnTheStartPageWithNoHistory()
    {
        PageNavigator navigator = new(_home);

        Assert.Multiple(() =>
        {
            Assert.That(navigator.CurrentPage, Is.SameAs(_home));
            Assert.That(navigator.CanGoBack, Is.False);
            Assert.That(navigator.GoBackCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void OpeningAPageRemembersWhereItCameFrom()
    {
        PageNavigator navigator = new(_home);

        navigator.Open(_detail);
        navigator.Open(_deeper);
        navigator.GoBack();

        Assert.That(navigator.CurrentPage, Is.SameAs(_detail));
    }

    [Test]
    public void GoingBackUnwindsTheWholeHistory()
    {
        PageNavigator navigator = new(_home);
        navigator.Open(_detail);
        navigator.Open(_deeper);

        navigator.GoBack();
        navigator.GoBack();

        Assert.Multiple(() =>
        {
            Assert.That(navigator.CurrentPage, Is.SameAs(_home));
            Assert.That(navigator.CanGoBack, Is.False);
        });
    }

    [Test]
    public void GoingBackWithNoHistoryStaysPut()
    {
        PageNavigator navigator = new(_home);

        navigator.GoBack();

        Assert.That(navigator.CurrentPage, Is.SameAs(_home));
    }

    [Test]
    public void OpeningThePageAlreadyShownAddsNoHistory()
    {
        PageNavigator navigator = new(_home);

        navigator.Open(_home);

        Assert.That(navigator.CanGoBack, Is.False);
    }

    [Test]
    public void ASectionStartsAFreshHistory()
    {
        PageNavigator navigator = new(_home);
        navigator.Open(_detail);

        navigator.ShowSection(_section);

        Assert.Multiple(() =>
        {
            Assert.That(navigator.CurrentPage, Is.SameAs(_section));
            Assert.That(navigator.CanGoBack, Is.False);
        });
    }

    [Test]
    public void ReturningToTheCurrentSectionStillForgetsTheHistory()
    {
        PageNavigator navigator = new(_home);
        navigator.Open(_detail);
        navigator.ShowSection(_section);
        navigator.Open(_deeper);
        navigator.GoBack();

        navigator.ShowSection(_section);

        Assert.That(navigator.CanGoBack, Is.False);
    }

    [Test]
    public void ThePageChangeAndTheBackStateAreAnnounced()
    {
        PageNavigator navigator = new(_home);
        List<string?> changed = [];
        int backChanges = 0;
        navigator.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        navigator.GoBackCommand.CanExecuteChanged += (_, _) => backChanges++;

        navigator.Open(_detail);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Does.Contain(nameof(PageNavigator.CurrentPage)));
            Assert.That(changed, Does.Contain(nameof(PageNavigator.CanGoBack)));
            Assert.That(backChanges, Is.GreaterThan(0));
        });
    }

    [Test]
    public void TheBackCommandGoesBack()
    {
        PageNavigator navigator = new(_home);
        navigator.Open(_detail);

        navigator.GoBackCommand.Execute(null);

        Assert.That(navigator.CurrentPage, Is.SameAs(_home));
    }

    private sealed class TestPage(string title) : PageViewModel
    {
        public override string Title { get; } = title;
    }
}
