using BlueH.Models;

namespace BlueH.Services;

public interface ISettingsStore
{
    SettingsLoad Load();

    void Save(AppSettings settings);
}
