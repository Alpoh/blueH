using System.Windows.Input;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DevicesPageViewModel : PageViewModel
{
    public DevicesPageViewModel(DevicesViewModel devices, PairingViewModel pairing)
    {
        Devices = devices;
        Pairing = pairing;
        AddDeviceCommand = new RelayCommand(_ => Pairing.Open());
    }

    public DevicesViewModel Devices { get; }

    public PairingViewModel Pairing { get; }

    public ICommand AddDeviceCommand { get; }

    public string AddDeviceLabel => AppResources.PairingHeader;

    public override string Title => AppResources.DevicesHeader;
}
