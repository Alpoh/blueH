using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using BlueH.Diagnostics;
using BlueH.Services;
using BlueH.Vendors.Oppo;
using BlueH.ViewModels;
using AppResources = BlueH.Properties.Resources;

namespace BlueH;

public partial class App
{
    private const string LoadSettingsOperation = "Load settings";
    private const string StartUpOperation = "Start up";
    private const string ApplyCultureOperation = "Apply user interface culture";
    private const string InitializeDevicesOperation = "Initialize device list";
    private const string InitializeMediaTransportOperation = "Initialize media transport";

    private MainViewModel? _mainViewModel;
    private WindowsMediaTransportService? _mediaTransportService;

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
        SettingsLoad settingsLoad = settingsStore.Load();
        RecordSettingsLoad(diagnosticsLog, settingsLoad);
        string? activeLanguageTag = CultureSelection.NormalizeSupportedTag(settingsLoad.Settings.LanguageTag);
        ApplyCultures(activeLanguageTag);
        diagnosticsLog.Record(DiagnosticOutcome.Information, ApplyCultureOperation, CultureInfo.CurrentUICulture.Name);

        DevicesViewModel devices = new(
            deviceService,
            batteryService,
            uiScheduler,
            diagnosticsLog,
            TimeProvider.System);
        CoreAudioEndpointService endpointService = new(diagnosticsLog);
        EndpointsViewModel allOutputs = new(endpointService, uiScheduler, diagnosticsLog);
        EndpointsViewModel deviceOutputs = new(endpointService, uiScheduler, diagnosticsLog);
        _mediaTransportService = new WindowsMediaTransportService(diagnosticsLog);
        MediaTransportViewModel mediaTransport = new(_mediaTransportService, uiScheduler, diagnosticsLog);

        WinRtBluetoothPairingService pairingService = new(diagnosticsLog);
        SelectedDeviceViewModel selectedDevice = new(
            new KernelStreamingBluetoothConnectionService(diagnosticsLog),
            pairingService,
            diagnosticsLog,
            TimeProvider.System);
        PairingViewModel pairing = new(pairingService, uiScheduler, diagnosticsLog, TimeProvider.System);

        _mainViewModel = new MainViewModel(
            devices,
            selectedDevice,
            new SettingsViewModel(
                settingsStore,
                diagnosticsLog,
                new AssemblyApplicationInfo(typeof(App).Assembly),
                new ShellExternalLinkOpener(),
                activeLanguageTag),
            pairing,
            allOutputs,
            deviceOutputs,
            mediaTransport,
            new NoiseControlViewModel(
                new OppoNoiseControlService(diagnosticsLog, TimeProvider.System),
                settingsStore,
                uiScheduler,
                diagnosticsLog),
            new DiagnosticsViewModel(diagnosticsLog, uiScheduler));

        MainWindow window = new()
        {
            Title = AppResources.ApplicationTitle,
            DataContext = _mainViewModel
        };

        window.Show();

        _ = InitializeAsync(devices, allOutputs, diagnosticsLog);
        _ = InitializeMediaTransportAsync(mediaTransport, diagnosticsLog);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _mainViewModel = null;
        _mediaTransportService?.Dispose();
        _mediaTransportService = null;

        base.OnExit(e);
    }

    private static async Task InitializeAsync(
        DevicesViewModel devices,
        EndpointsViewModel endpoints,
        IDiagnosticsLog diagnosticsLog)
    {
        try
        {
            await devices.RefreshAsync(CancellationToken.None);
            await endpoints.RefreshAsync(CancellationToken.None);
            devices.StartWatching();
            devices.StartBatteryPolling(DevicesViewModel.DefaultBatteryPollingInterval);
        }
        catch (Exception failure)
        {
            diagnosticsLog.Record(DiagnosticOutcome.Failure, InitializeDevicesOperation, failure.Message);
        }
    }

    private static async Task InitializeMediaTransportAsync(
        MediaTransportViewModel mediaTransport,
        IDiagnosticsLog diagnosticsLog)
    {
        try
        {
            await mediaTransport.StartAsync(CancellationToken.None);
        }
        catch (Exception failure)
        {
            diagnosticsLog.Record(DiagnosticOutcome.Failure, InitializeMediaTransportOperation, failure.Message);
        }
    }

    private static void ApplyCultures(string? languageTag)
    {
        CultureInfo uiCulture = CultureSelection.ResolveUiCulture(languageTag, CultureInfo.CurrentUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        CultureInfo.CurrentUICulture = uiCulture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
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
