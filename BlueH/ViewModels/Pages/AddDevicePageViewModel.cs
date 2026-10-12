using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class AddDevicePageViewModel : PageViewModel
{
    public AddDevicePageViewModel(PairingViewModel pairing)
    {
        Pairing = pairing;
    }

    public PairingViewModel Pairing { get; }

    public override string Title => AppResources.PairingHeader;
}
