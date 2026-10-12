using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class SettingsViewModelTests
{

    private FakeSettingsStore _settingsStore = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsStore = new FakeSettingsStore();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
    }

    [Test]
    public void TheOptionsAreTheSystemDefaultFollowedByEverySupportedLanguage()
    {
        SettingsViewModel viewModel = CreateViewModel(activeLanguageTag: null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Languages[0].LanguageTag, Is.Null);
            Assert.That(
                viewModel.Languages.Skip(1).Select(option => option.LanguageTag),
                Is.EqualTo(CultureSelection.SupportedLanguageTags));
            Assert.That(viewModel.Languages.Select(option => option.DisplayName), Is.All.Not.Empty);
            Assert.That(viewModel.Languages.Select(option => option.DisplayName), Is.Unique);
        });
    }

    [Test]
    public void SpanishIsNamedInSpanish()
    {
        SettingsViewModel viewModel = CreateViewModel(activeLanguageTag: null);

        LanguageOption spanish = viewModel.Languages.Single(option => option.LanguageTag == CultureSelection.SpanishTag);

        Assert.That(spanish.DisplayName, Does.StartWith("Español"));
    }

    [Test]
    public void TheStoredLanguageIsSelected()
    {
        _settingsStore.Settings = new AppSettings(CultureSelection.SpanishTag);

        SettingsViewModel viewModel = CreateViewModel(CultureSelection.SpanishTag);

        Assert.That(viewModel.SelectedLanguage.LanguageTag, Is.EqualTo(CultureSelection.SpanishTag));
    }

    [TestCase("tlh-KX")]
    [TestCase("not a culture")]
    [TestCase("")]
    public void AnUnsupportedStoredLanguageSelectsTheSystemDefault(string storedTag)
    {
        _settingsStore.Settings = new AppSettings(storedTag);

        SettingsViewModel viewModel = CreateViewModel(activeLanguageTag: null);

        Assert.That(viewModel.SelectedLanguage.LanguageTag, Is.Null);
    }

    [Test]
    public void ChoosingALanguageSavesIt()
    {
        SettingsViewModel viewModel = CreateViewModel(activeLanguageTag: null);

        viewModel.SelectedLanguage = Option(viewModel, CultureSelection.SpanishTag);

        Assert.That(_settingsStore.Settings.LanguageTag, Is.EqualTo(CultureSelection.SpanishTag));
    }

    [Test]
    public void ARestartIsRequiredOnlyWhenTheChoiceDiffersFromTheRunningLanguage()
    {
        SettingsViewModel viewModel = CreateViewModel(CultureSelection.EnglishTag);
        bool requiredAtStart = viewModel.IsRestartRequired;

        viewModel.SelectedLanguage = Option(viewModel, CultureSelection.SpanishTag);
        bool requiredAfterChange = viewModel.IsRestartRequired;
        viewModel.SelectedLanguage = Option(viewModel, CultureSelection.EnglishTag);

        Assert.Multiple(() =>
        {
            Assert.That(requiredAtStart, Is.True, "the stored default differs from the running English");
            Assert.That(requiredAfterChange, Is.True);
            Assert.That(viewModel.IsRestartRequired, Is.False);
        });
    }

    [Test]
    public void AFailureToSaveIsShownAndLogged()
    {
        _settingsStore.SaveFailureToThrow = new IOException("the disk is full");
        SettingsViewModel viewModel = CreateViewModel(activeLanguageTag: null);

        viewModel.SelectedLanguage = Option(viewModel, CultureSelection.SpanishTag);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorDetail, Does.Contain("the disk is full"));
            Assert.That(_diagnosticsLog.Entries[0].Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
        });
    }

    private static LanguageOption Option(SettingsViewModel viewModel, string languageTag)
    {
        return viewModel.Languages.Single(option => option.LanguageTag == languageTag);
    }

    private SettingsViewModel CreateViewModel(string? activeLanguageTag)
    {
        return new SettingsViewModel(_settingsStore, _diagnosticsLog, activeLanguageTag);
    }
}
