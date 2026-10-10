using System.Windows;
using BlueH.Diagnostics;
using BlueH.Services;
using BlueH.ViewModels;
using AppResources = BlueH.Properties.Resources;

namespace BlueH;

public partial class App
{
    private const string LoadSettingsOperation = "Load settings";
    private const string StartUpOperation = "Start up";
    private const string InitializeDevicesOperation = "Initialize device list";

    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InMemoryDiagnosticsLog diagnosticsLog = new(TimeProvider.System);
        DispatcherUiScheduler uiScheduler = new(Dispatcher);
        JsonSettingsStore settingsStore = new(JsonSettingsStore.DefaultFilePath);
        WinRtBluetoothDeviceService deviceService = new(diagnosticsLog);
        FirstKnownBatteryService batteryService = new(
        [
            new DevicePropertyBatteryService(diagnosticsLog),
            new GattBatteryService(diagnosticsLog)
        ]);

        diagnosticsLog.Record(DiagnosticOutcome.Information, StartUpOperation, JsonSettingsStore.DefaultFilePath);
        RecordSettingsLoad(diagnosticsLog, settingsStore.Load());

        DevicesViewModel devices = new(
            deviceService,
            batteryService,
            uiScheduler,
            diagnosticsLog,
            settingsStore,
            TimeProvider.System);
        _mainViewModel = new MainViewModel(devices, new DiagnosticsViewModel(diagnosticsLog, uiScheduler));

        MainWindow window = new()
        {
            Title = AppResources.ApplicationTitle,
            DataContext = _mainViewModel
        };

        window.Show();

        _ = InitializeDevicesAsync(devices, diagnosticsLog);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _mainViewModel = null;

        base.OnExit(e);
    }

    private static async Task InitializeDevicesAsync(DevicesViewModel devices, IDiagnosticsLog diagnosticsLog)
    {
        try
        {
            await devices.RefreshAsync(CancellationToken.None);
            devices.StartWatching();
            devices.StartBatteryPolling(DevicesViewModel.DefaultBatteryPollingInterval);
        }
        catch (Exception failure)
        {
            diagnosticsLog.Record(DiagnosticOutcome.Failure, InitializeDevicesOperation, failure.Message);
        }
    }

    private static void RecordSettingsLoad(IDiagnosticsLog diagnosticsLog, SettingsLoad load)
    {
        DiagnosticOutcome outcome = load.Source switch
        {
            SettingsSource.File => DiagnosticOutcome.Success,
            SettingsSource.DefaultsBecauseMissing => DiagnosticOutcome.Information,
            _ => DiagnosticOutcome.Failure
        };

        diagnosticsLog.Record(outcome, LoadSettingsOperation, load.FailureDetail ?? load.Source.ToString());
    }
}
