using System.Collections.ObjectModel;
using System.ComponentModel;
using BlueH.Themes;
using BlueH.ViewModels.Pages;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private NavigationItem? _selectedMenuItem;
    private PageViewModel? _shownPage;
    private readonly NavigationItem _settingsItem;
    private readonly NavigationItem _diagnosticsItem;

    public MainViewModel(
        DevicesViewModel devices,
        SelectedDeviceViewModel selectedDevice,
        SettingsViewModel settings,
        PairingViewModel pairing,
        EndpointsViewModel allOutputs,
        EndpointsViewModel deviceOutputs,
        MediaTransportViewModel mediaTransport,
        NoiseControlViewModel noiseControl,
        DiagnosticsViewModel diagnostics,
        DisplayModeViewModel displayMode)
    {
        Devices = devices;
        DisplayMode = displayMode;
        SelectedDevice = selectedDevice;
        Pairing = pairing;
        AllOutputs = allOutputs;
        DeviceOutputs = deviceOutputs;
        MediaTransport = mediaTransport;
        Diagnostics = diagnostics;
        SelectedDevice.Device = devices.SelectedDevice;

        DevicesPage = new DevicesPageViewModel(devices);
        AddDevicePage = new AddDevicePageViewModel(pairing);
        DevicePage = new DevicePageViewModel(selectedDevice, deviceOutputs, noiseControl);
        AudioPage = new AudioPageViewModel(allOutputs);
        SettingsPage = new SettingsPageViewModel(settings);
        DiagnosticsPage = new DiagnosticsPageViewModel(diagnostics);
        _settingsItem = new NavigationItem(Glyphs.Settings, AppResources.SettingsHeader, SettingsPage);
        _diagnosticsItem = new NavigationItem(Glyphs.Diagnostics, AppResources.DiagnosticsHeader, DiagnosticsPage);
        MenuItems =
        [
            new NavigationItem(Glyphs.Devices, AppResources.DevicesHeader, DevicesPage),
            new NavigationItem(Glyphs.Volume, AppResources.EndpointsHeader, AudioPage),
            _settingsItem
        ];
        Navigator = new PageNavigator(DevicesPage);
        _shownPage = DevicesPage;
        _selectedMenuItem = MenuItems[0];

        Devices.PropertyChanged += OnDevicesPropertyChanged;
        Devices.ConnectRequested += OnConnectRequested;
        DevicesPage.AddDeviceRequested += OnAddDeviceRequested;
        Settings = settings;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
        ApplyDeveloperMode();
        Pairing.Paired += OnPaired;
        Navigator.PropertyChanged += OnNavigatorPropertyChanged;
    }

    public DevicesViewModel Devices { get; }

    public SelectedDeviceViewModel SelectedDevice { get; }

    public SettingsViewModel Settings { get; }

    public PairingViewModel Pairing { get; }

    public EndpointsViewModel AllOutputs { get; }

    public EndpointsViewModel DeviceOutputs { get; }

    public MediaTransportViewModel MediaTransport { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public DisplayModeViewModel DisplayMode { get; }

    public DevicesPageViewModel DevicesPage { get; }

    public AddDevicePageViewModel AddDevicePage { get; }

    public DevicePageViewModel DevicePage { get; }

    public AudioPageViewModel AudioPage { get; }

    public SettingsPageViewModel SettingsPage { get; }

    public DiagnosticsPageViewModel DiagnosticsPage { get; }

    public ObservableCollection<NavigationItem> MenuItems { get; }

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
        DevicesPage.AddDeviceRequested -= OnAddDeviceRequested;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Pairing.Paired -= OnPaired;
        Navigator.PropertyChanged -= OnNavigatorPropertyChanged;
        DevicePage.Dispose();
        Devices.Dispose();
        AllOutputs.Dispose();
        DeviceOutputs.Dispose();
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

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.IsDeveloperModeEnabled))
        {
            ApplyDeveloperMode();
        }
    }

    private void ApplyDeveloperMode()
    {
        bool isShown = MenuItems.Contains(_diagnosticsItem);

        if (Settings.IsDeveloperModeEnabled && !isShown)
        {
            MenuItems.Add(_diagnosticsItem);
        }
        else if (!Settings.IsDeveloperModeEnabled && isShown)
        {
            if (ReferenceEquals(Navigator.CurrentPage, DiagnosticsPage))
            {
                SelectedMenuItem = _settingsItem;
            }

            MenuItems.Remove(_diagnosticsItem);
        }
    }

    private void OnAddDeviceRequested(object? sender, EventArgs e)
    {
        Navigator.Open(AddDevicePage);
        Pairing.BeginSearching();
    }

    private void OnPaired(object? sender, EventArgs e)
    {
        if (ReferenceEquals(Navigator.CurrentPage, AddDevicePage))
        {
            Navigator.GoBack();
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

        if (ReferenceEquals(_shownPage, AddDevicePage) && !ReferenceEquals(Navigator.CurrentPage, AddDevicePage))
        {
            Pairing.EndSearching();
        }

        _shownPage = Navigator.CurrentPage;
    }
}
