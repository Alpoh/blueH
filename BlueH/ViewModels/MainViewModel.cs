namespace BlueH.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    public MainViewModel(DevicesViewModel devices, DiagnosticsViewModel diagnostics)
    {
        Devices = devices;
        Diagnostics = diagnostics;
    }

    public DevicesViewModel Devices { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public void Dispose()
    {
        Devices.Dispose();
        Diagnostics.Dispose();
    }
}
