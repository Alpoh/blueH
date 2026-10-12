using System.Windows.Input;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DevicesPageViewModel : PageViewModel
{
    public DevicesPageViewModel(DevicesViewModel devices)
    {
        Devices = devices;
        AddDeviceCommand = new RelayCommand(_ => AddDeviceRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? AddDeviceRequested;

    public DevicesViewModel Devices { get; }

    public ICommand AddDeviceCommand { get; }

    public string AddDeviceLabel => AppResources.PairingHeader;

    public override string Title => AppResources.DevicesHeader;
}
