using System.ComponentModel;

namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel(
        DevicesViewModel devices,
        EndpointsViewModel endpoints,
        MediaTransportViewModel mediaTransport,
        DiagnosticsViewModel diagnostics)
    {
        Devices = devices;
        Endpoints = endpoints;
        MediaTransport = mediaTransport;
        Diagnostics = diagnostics;
        Devices.PropertyChanged += OnDevicesPropertyChanged;
    }

    public DevicesViewModel Devices { get; }

    public EndpointsViewModel Endpoints { get; }

    public MediaTransportViewModel MediaTransport { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public void Dispose()
    {
        Devices.PropertyChanged -= OnDevicesPropertyChanged;
        Devices.Dispose();
        MediaTransport.Dispose();
        Diagnostics.Dispose();
    }

    private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(DevicesViewModel.SelectedContainerId)
            or nameof(DevicesViewModel.ConnectedContainerIds)))
        {
            return;
        }

        Endpoints.ContainerFilter = Devices.SelectedContainerId;
        _ = Endpoints.RefreshAsync(CancellationToken.None);
    }
}
