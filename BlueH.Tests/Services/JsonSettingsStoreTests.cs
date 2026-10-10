using System.IO;
using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class JsonSettingsStoreTests
{
    private const string AnyDeviceId = "BTHENUM\\{0000110b-0000-1000-8000-00805f9b34fb}_LOCALMFG&000f";

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
        store.Save(new AppSettings(AnyDeviceId));

        Assert.That(store.Load().Settings.SelectedDeviceId, Is.EqualTo(AnyDeviceId));
    }

    [Test]
    public void SaveThenLoadRoundTripsAnAbsentSelectedDevice()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(SelectedDeviceId: null));

        Assert.That(store.Load().Settings.SelectedDeviceId, Is.Null);
    }

    [Test]
    public void LoadReportsTheFileAsTheSourceWhenItWasRead()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(AnyDeviceId));

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

        new JsonSettingsStore(nestedPath).Save(new AppSettings(AnyDeviceId));

        Assert.That(File.Exists(nestedPath), Is.True);
    }

    [Test]
    public void SaveOverwritesAPreviousValue()
    {
        JsonSettingsStore store = new(_filePath);
        store.Save(new AppSettings(AnyDeviceId));

        store.Save(new AppSettings("a-different-device"));

        Assert.That(store.Load().Settings.SelectedDeviceId, Is.EqualTo("a-different-device"));
    }

    [Test]
    public void SaveWritesCamelCasedKeysSoTheFileStaysAStableContract()
    {
        new JsonSettingsStore(_filePath).Save(new AppSettings(AnyDeviceId));

        Assert.That(File.ReadAllText(_filePath), Does.Contain("\"selectedDeviceId\""));
    }

    [Test]
    public void SaveWritesIndentedJsonSoTheFileCanBeReadByHand()
    {
        new JsonSettingsStore(_filePath).Save(new AppSettings(AnyDeviceId));

        Assert.That(File.ReadAllText(_filePath), Does.Contain(Environment.NewLine));
    }

    [Test]
    public void SaveFailsLoudlyWhenThePathCannotBeWritten()
    {
        string blocker = Path.Combine(_temporaryDirectory, "blocker");
        File.WriteAllText(blocker, string.Empty);
        JsonSettingsStore store = new(Path.Combine(blocker, "settings.json"));

        Assert.That(() => store.Save(new AppSettings(AnyDeviceId)), Throws.InstanceOf<IOException>());
    }
}
