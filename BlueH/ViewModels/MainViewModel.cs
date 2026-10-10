using System.ComponentModel;

namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel(
        DevicesViewModel devices,
        EndpointsViewModel endpoints,
        DiagnosticsViewModel diagnostics)
    {
        Devices = devices;
        Endpoints = endpoints;
        Diagnostics = diagnostics;
        Devices.PropertyChanged += OnDevicesPropertyChanged;
    }

    public DevicesViewModel Devices { get; }

    public EndpointsViewModel Endpoints { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public void Dispose()
    {
        Devices.PropertyChanged -= OnDevicesPropertyChanged;
        Devices.Dispose();
        Diagnostics.Dispose();
    }

    private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DevicesViewModel.SelectedContainerId))
        {
            return;
        }

        Endpoints.ContainerFilter = Devices.SelectedContainerId;
        _ = Endpoints.RefreshAsync(CancellationToken.None);
    }
}
