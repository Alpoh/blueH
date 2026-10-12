using System.ComponentModel;
using BlueH.Themes;
using BlueH.ViewModels.Pages;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private NavigationItem? _selectedMenuItem;

    public MainViewModel(
        DevicesViewModel devices,
        SelectedDeviceViewModel selectedDevice,
        SettingsViewModel settings,
        PairingViewModel pairing,
        EndpointsViewModel allOutputs,
        EndpointsViewModel deviceOutputs,
        MediaTransportViewModel mediaTransport,
        DiagnosticsViewModel diagnostics)
    {
        Devices = devices;
        SelectedDevice = selectedDevice;
        Pairing = pairing;
        AllOutputs = allOutputs;
        DeviceOutputs = deviceOutputs;
        MediaTransport = mediaTransport;
        Diagnostics = diagnostics;
        SelectedDevice.Device = devices.SelectedDevice;

        DevicesPage = new DevicesPageViewModel(devices, pairing);
        DevicePage = new DevicePageViewModel(selectedDevice, deviceOutputs);
        AudioPage = new AudioPageViewModel(allOutputs);
        SettingsPage = new SettingsPageViewModel(settings);
        DiagnosticsPage = new DiagnosticsPageViewModel(diagnostics);
        MenuItems =
        [
            new NavigationItem(Glyphs.Devices, AppResources.DevicesHeader, DevicesPage),
            new NavigationItem(Glyphs.Volume, AppResources.EndpointsHeader, AudioPage),
            new NavigationItem(Glyphs.Settings, AppResources.SettingsHeader, SettingsPage),
            new NavigationItem(Glyphs.Diagnostics, AppResources.DiagnosticsHeader, DiagnosticsPage)
        ];
        Navigator = new PageNavigator(DevicesPage);
        _selectedMenuItem = MenuItems[0];

        Devices.PropertyChanged += OnDevicesPropertyChanged;
        Devices.ConnectRequested += OnConnectRequested;
        Navigator.PropertyChanged += OnNavigatorPropertyChanged;
    }

    public DevicesViewModel Devices { get; }

    public SelectedDeviceViewModel SelectedDevice { get; }

    public PairingViewModel Pairing { get; }

    public EndpointsViewModel AllOutputs { get; }

    public EndpointsViewModel DeviceOutputs { get; }

    public MediaTransportViewModel MediaTransport { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public DevicesPageViewModel DevicesPage { get; }

    public DevicePageViewModel DevicePage { get; }

    public AudioPageViewModel AudioPage { get; }

    public SettingsPageViewModel SettingsPage { get; }

    public DiagnosticsPageViewModel DiagnosticsPage { get; }

    public IReadOnlyList<NavigationItem> MenuItems { get; }

    public PageNavigator Navigator { get; }

    public string BackLabel => AppResources.NavigationBackLabel;

    public NavigationItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (SetField(ref _selectedMenuItem, value) && value is not null)
            {
                Navigator.ShowSection(value.Page);
            }
        }
    }

    public void Dispose()
    {
        Devices.PropertyChanged -= OnDevicesPropertyChanged;
        Devices.ConnectRequested -= OnConnectRequested;
        Navigator.PropertyChanged -= OnNavigatorPropertyChanged;
        DevicePage.Dispose();
        Devices.Dispose();
        MediaTransport.Dispose();
        Pairing.Dispose();
        Diagnostics.Dispose();
    }

    private void OnDevicesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DevicesViewModel.SelectedDevice):
                SelectedDevice.Device = Devices.SelectedDevice;
                break;
            case nameof(DevicesViewModel.SelectedContainerId) when Devices.SelectedContainerId is Guid containerId:
                OpenDevice(containerId);
                break;
            case nameof(DevicesViewModel.ConnectedContainerIds):
                _ = AllOutputs.RefreshAsync(CancellationToken.None);
                _ = DeviceOutputs.RefreshAsync(CancellationToken.None);
                break;
        }
    }

    private void OnConnectRequested(object? sender, Guid containerId)
    {
        Devices.SelectedContainerId = containerId;

        if (SelectedDevice.ConnectCommand.CanExecute(null))
        {
            SelectedDevice.ConnectCommand.Execute(null);
        }
    }

    private void OpenDevice(Guid containerId)
    {
        DeviceOutputs.ContainerFilter = containerId;
        _ = DeviceOutputs.RefreshAsync(CancellationToken.None);
        Navigator.Open(DevicePage);
    }

    private void OnNavigatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageNavigator.CurrentPage))
        {
            return;
        }

        if (!ReferenceEquals(Navigator.CurrentPage, DevicePage))
        {
            Devices.SelectedContainerId = null;
        }
    }
}
