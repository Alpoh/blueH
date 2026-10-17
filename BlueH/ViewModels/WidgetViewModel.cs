using System.Collections.Specialized;
using System.ComponentModel;
using BlueH.Models;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class WidgetViewModel : ViewModelBase, IDisposable
{
    private readonly DevicesViewModel _devices;
    private readonly SelectedDeviceViewModel _selectedDevice;
    private readonly EndpointsViewModel _outputs;
    private readonly SettingsViewModel _settings;

    private BluetoothAudioDevice? _device;
    private AudioEndpointViewModel? _volume;

    public WidgetViewModel(
        DevicesViewModel devices,
        SelectedDeviceViewModel selectedDevice,
        MediaTransportViewModel media,
        NoiseControlViewModel noiseControl,
        EndpointsViewModel outputs,
        DisplayModeViewModel displayMode,
        SettingsViewModel settings)
    {
        _devices = devices;
        _selectedDevice = selectedDevice;
        _outputs = outputs;
        _settings = settings;
        Media = media;
        NoiseControl = noiseControl;
        DisplayMode = displayMode;

        _devices.Devices.CollectionChanged += OnDevicesChanged;
        _selectedDevice.PropertyChanged += OnSelectedDevicePropertyChanged;
        _outputs.Endpoints.CollectionChanged += OnOutputsChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        FollowFirstConnectedDevice();
    }

    public BluetoothAudioDevice? Device
    {
        get => _device;
        private set
        {
            if (SetField(ref _device, value))
            {
                OnPropertyChanged(nameof(HasDevice));
                NoiseControl.Device = value;
                UpdateSuspension();
                FollowOutputsOf(value);
            }
        }
    }

    public bool HasDevice => _device is not null;

    public AudioEndpointViewModel? Volume
    {
        get => _volume;
        private set
        {
            if (SetField(ref _volume, value))
            {
                OnPropertyChanged(nameof(HasVolume));
            }
        }
    }

    public bool HasVolume => _volume is not null;

    public MediaTransportViewModel Media { get; }

    public NoiseControlViewModel NoiseControl { get; }

    public DisplayModeViewModel DisplayMode { get; }

    public bool IsTopmost => _settings.IsWidgetTopmost;

    public string NoDeviceMessage => AppResources.WidgetNoDeviceMessage;

    public string VolumeLabel => AppResources.EndpointVolumeHeader;

    public string MuteLabel => AppResources.EndpointMuteLabel;

    public string BatteryUnknownShortLabel => AppResources.DeviceBatteryUnknownShortLabel;

    public string BatteryUnknownTooltip => AppResources.DeviceBatteryUnknownTooltip;

    public void RememberPosition(WidgetPosition position)
    {
        _settings.RememberWidgetPosition(position);
    }

    public void Dispose()
    {
        _devices.Devices.CollectionChanged -= OnDevicesChanged;
        _selectedDevice.PropertyChanged -= OnSelectedDevicePropertyChanged;
        _outputs.Endpoints.CollectionChanged -= OnOutputsChanged;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        NoiseControl.Dispose();
    }

    private void FollowFirstConnectedDevice()
    {
        Device = _devices.Devices.FirstOrDefault(device => device.IsConnected == true);
    }

    private void FollowOutputsOf(BluetoothAudioDevice? device)
    {
        if (device is null)
        {
            ChooseVolume();

            return;
        }

        _outputs.ContainerFilter = device.ContainerId;
        _ = _outputs.RefreshAsync(CancellationToken.None);
    }

    private void ChooseVolume()
    {
        AudioEndpointViewModel[] deviceOutputs =
        [
            .. _outputs.Endpoints.Where(endpoint => _device is not null && endpoint.ContainerId == _device.ContainerId)
        ];

        Volume = deviceOutputs.FirstOrDefault(endpoint => endpoint.IsDefaultForPlayback) ?? deviceOutputs.FirstOrDefault();
    }

    private void UpdateSuspension()
    {
        NoiseControl.IsSuspended =
            _selectedDevice.Activity is ConnectionActivity.Disconnecting or ConnectionActivity.Unpairing
            && _device is not null
            && _selectedDevice.Device?.ContainerId == _device.ContainerId;
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        FollowFirstConnectedDevice();
    }

    private void OnOutputsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ChooseVolume();
    }

    private void OnSelectedDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SelectedDeviceViewModel.Activity) or nameof(SelectedDeviceViewModel.Device))
        {
            UpdateSuspension();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.IsWidgetTopmost))
        {
            OnPropertyChanged(nameof(IsTopmost));
        }
    }
}
