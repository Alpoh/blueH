using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DevicesPageViewModel : PageViewModel
{
    public DevicesPageViewModel(DevicesViewModel devices, PairingViewModel pairing)
    {
        Devices = devices;
        Pairing = pairing;
    }

    public DevicesViewModel Devices { get; }

    public PairingViewModel Pairing { get; }

    public override string Title => AppResources.DevicesHeader;
}
