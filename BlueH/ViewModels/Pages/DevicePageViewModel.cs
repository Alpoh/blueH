using System.ComponentModel;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DevicePageViewModel : PageViewModel, IDisposable
{
    public DevicePageViewModel(SelectedDeviceViewModel device, EndpointsViewModel outputs, NoiseControlViewModel noiseControl)
    {
        Device = device;
        Outputs = outputs;
        NoiseControl = noiseControl;
        NoiseControl.Device = Device.Device;
        Device.PropertyChanged += OnDevicePropertyChanged;
    }

    public SelectedDeviceViewModel Device { get; }

    public EndpointsViewModel Outputs { get; }

    public NoiseControlViewModel NoiseControl { get; }

    public override string Title => Device.Device?.Name ?? string.Empty;

    public string OutputsHeader => AppResources.EndpointsHeader;

    public void Dispose()
    {
        Device.PropertyChanged -= OnDevicePropertyChanged;
        NoiseControl.Dispose();
    }

    private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedDeviceViewModel.Device))
        {
            NoiseControl.Device = Device.Device;
            OnPropertyChanged(nameof(Title));
        }
    }
}
