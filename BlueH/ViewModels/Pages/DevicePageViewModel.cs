using System.ComponentModel;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DevicePageViewModel : PageViewModel, IDisposable
{
    public DevicePageViewModel(SelectedDeviceViewModel device, EndpointsViewModel outputs)
    {
        Device = device;
        Outputs = outputs;
        Device.PropertyChanged += OnDevicePropertyChanged;
    }

    public SelectedDeviceViewModel Device { get; }

    public EndpointsViewModel Outputs { get; }

    public override string Title => Device.Device?.Name ?? string.Empty;

    public string OutputsHeader => AppResources.EndpointsHeader;

    public void Dispose()
    {
        Device.PropertyChanged -= OnDevicePropertyChanged;
    }

    private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedDeviceViewModel.Device))
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
