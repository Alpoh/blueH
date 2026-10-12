using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class SettingsPageViewModel : PageViewModel
{
    public SettingsPageViewModel(SettingsViewModel settings)
    {
        Settings = settings;
    }

    public SettingsViewModel Settings { get; }

    public override string Title => AppResources.SettingsHeader;
}
