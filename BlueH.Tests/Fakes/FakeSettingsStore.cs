using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Settings { get; set; } = AppSettings.Default;

    public SettingsSource Source { get; set; } = SettingsSource.File;

    public Exception? SaveFailureToThrow { get; set; }

    public int SaveCount { get; private set; }

    public SettingsLoad Load()
    {
        return new SettingsLoad(Settings, Source, FailureDetail: null);
    }

    public void Save(AppSettings settings)
    {
        SaveCount++;

        if (SaveFailureToThrow is not null)
        {
            throw SaveFailureToThrow;
        }

        Settings = settings;
    }
}
