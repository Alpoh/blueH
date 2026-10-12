using System.IO;
using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class JsonSettingsStoreTests
{
    private const string AnyLanguageTag = "es-ES";
    private const string OtherLanguageTag = "en-US";

    private string _temporaryDirectory = null!;
    private string _filePath = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"BlueH.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _filePath = Path.Combine(_temporaryDirectory, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void SaveThenLoadRoundTripsTheSelectedDevice()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(AnyLanguageTag));

        Assert.That(store.Load().Settings.LanguageTag, Is.EqualTo(AnyLanguageTag));
    }

    [Test]
    public void SaveThenLoadRoundTripsAnAbsentSelectedDevice()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(LanguageTag: null));

        Assert.That(store.Load().Settings.LanguageTag, Is.Null);
    }

    [Test]
    public void LoadReportsTheFileAsTheSourceWhenItWasRead()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(AnyLanguageTag));

        Assert.That(store.Load().Source, Is.EqualTo(SettingsSource.File));
    }

    [Test]
    public void LoadFallsBackToDefaultsWhenThereIsNoFile()
    {
        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.Settings, Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public void LoadSaysWhyItFellBackWhenThereIsNoFile()
    {
        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.Source, Is.EqualTo(SettingsSource.DefaultsBecauseMissing));
    }

    [Test]
    public void LoadReportsNoFailureDetailWhenThereIsSimplyNoFile()
    {
        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.FailureDetail, Is.Null);
    }

    [Test]
    public void LoadFallsBackToDefaultsWhenTheFileIsNotJson()
    {
        File.WriteAllText(_filePath, "this is not json {{{");

        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.Settings, Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public void LoadDistinguishesAnUnreadableFileFromAMissingOne()
    {
        File.WriteAllText(_filePath, "this is not json {{{");

        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.Source, Is.EqualTo(SettingsSource.DefaultsBecauseUnreadable));
    }

    [Test]
    public void LoadCarriesTheFailureDetailWhenTheFileIsUnreadable()
    {
        File.WriteAllText(_filePath, "this is not json {{{");

        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.FailureDetail, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LoadTreatsAJsonNullDocumentAsUnreadable()
    {
        File.WriteAllText(_filePath, "null");

        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.That(load.Source, Is.EqualTo(SettingsSource.DefaultsBecauseUnreadable));
    }

    [Test]
    public void LoadNeverThrowsOnAnUnreadableFile()
    {
        File.WriteAllText(_filePath, "this is not json {{{");
        JsonSettingsStore store = new(_filePath);

        Assert.That(() => store.Load(), Throws.Nothing);
    }

    [Test]
    public void SaveCreatesTheDirectoryWhenItIsMissing()
    {
        string nestedPath = Path.Combine(_temporaryDirectory, "nested", "deeper", "settings.json");

        new JsonSettingsStore(nestedPath).Save(new AppSettings(AnyLanguageTag));

        Assert.That(File.Exists(nestedPath), Is.True);
    }

    [Test]
    public void AFileFromAnOlderVersionStillLoads()
    {
        File.WriteAllText(_filePath, "{ \"selectedDeviceId\": \"0ac66fc7-8a44-50f8-ae2b-25ffdd18b301\" }");

        SettingsLoad load = new JsonSettingsStore(_filePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(load.Source, Is.EqualTo(SettingsSource.File));
            Assert.That(load.Settings.LanguageTag, Is.Null);
        });
    }

    [Test]
    public void SaveOverwritesAPreviousValue()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(AnyLanguageTag));

        store.Save(new AppSettings(OtherLanguageTag));

        Assert.That(store.Load().Settings.LanguageTag, Is.EqualTo(OtherLanguageTag));
    }

    [Test]
    public void SaveWritesCamelCasedKeysSoTheFileStaysAStableContract()
    {
        new JsonSettingsStore(_filePath).Save(new AppSettings(AnyLanguageTag));

        Assert.That(File.ReadAllText(_filePath), Does.Contain("\"languageTag\""));
    }

    [Test]
    public void SaveWritesIndentedJsonSoTheFileCanBeReadByHand()
    {
        new JsonSettingsStore(_filePath).Save(new AppSettings(AnyLanguageTag));

        Assert.That(File.ReadAllText(_filePath), Does.Contain(Environment.NewLine));
    }

    [Test]
    public void SaveFailsLoudlyWhenThePathCannotBeWritten()
    {
        string blocker = Path.Combine(_temporaryDirectory, "blocker");
        File.WriteAllText(blocker, string.Empty);
        JsonSettingsStore store = new(Path.Combine(blocker, "settings.json"));

        Assert.That(() => store.Save(new AppSettings(AnyLanguageTag)), Throws.InstanceOf<IOException>());
    }
}
