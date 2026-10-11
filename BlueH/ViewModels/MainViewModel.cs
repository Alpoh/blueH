using System.ComponentModel;

namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel(
        DevicesViewModel devices,
        SelectedDeviceViewModel selectedDevice,
        SettingsViewModel settings,
        PairingViewModel pairing,
        EndpointsViewModel endpoints,
        MediaTransportViewModel mediaTransport,
        DiagnosticsViewModel diagnostics)
    {
        Devices = devices;
        SelectedDevice = selectedDevice;
        Settings = settings;
        Pairing = pairing;
        SelectedDevice.Device = devices.SelectedDevice;
        Endpoints = endpoints;
        MediaTransport = mediaTransport;
        Diagnostics = diagnostics;
        Devices.PropertyChanged += OnDevicesPropertyChanged;
    }

    public DevicesViewModel Devices { get; }

    public SelectedDeviceViewModel SelectedDevice { get; }

    public SettingsViewModel Settings { get; }

    public PairingViewModel Pairing { get; }

    public EndpointsViewModel Endpoints { get; }

    public MediaTransportViewModel MediaTransport { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public void Dispose()
    {
        Devices.PropertyChanged -= OnDevicesPropertyChanged;
        Devices.Dispose();
        MediaTransport.Dispose();
        Pairing.Dispose();
        Diagnostics.Dispose();
    }

    private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DevicesViewModel.SelectedDevice))
        {
            SelectedDevice.Device = Devices.SelectedDevice;

            return;
        }

        if (e.PropertyName is not (nameof(DevicesViewModel.SelectedContainerId)
            or nameof(DevicesViewModel.ConnectedContainerIds)))
        {
            return;
        }

        Endpoints.ContainerFilter = Devices.SelectedContainerId;
        _ = Endpoints.RefreshAsync(CancellationToken.None);
    }
}
