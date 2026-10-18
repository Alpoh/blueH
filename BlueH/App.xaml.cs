using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using BlueH.Vendors.Oppo;
using BlueH.ViewModels;
using BlueH.Views;
using AppResources = BlueH.Properties.Resources;

namespace BlueH;

public partial class App
{
    private const string LoadSettingsOperation = "Load settings";
    private const string StartUpOperation = "Start up";
    private const string ApplyCultureOperation = "Apply user interface culture";
    private const string InitializeDevicesOperation = "Initialize device list";
    private const string InitializeMediaTransportOperation = "Initialize media transport";

    private const double WidgetScreenMargin = 24;
    private const string WidgetMaxWidthKey = "WidgetMaxWidth";

    private MainViewModel? _mainViewModel;
    private WidgetViewModel? _widgetViewModel;
    private SettingsViewModel? _settings;
    private DisplayModeViewModel? _displayMode;
    private MainWindow? _mainWindow;
    private bool _isExiting;
    private WidgetWindow? _widgetWindow;
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
        EndpointsViewModel widgetOutputs = new(endpointService, uiScheduler, diagnosticsLog);
        allOutputs.StartWatching();
        deviceOutputs.StartWatching();
        widgetOutputs.StartWatching();
        _mediaTransportService = new WindowsMediaTransportService(diagnosticsLog);
        MediaTransportViewModel mediaTransport = new(_mediaTransportService, uiScheduler, diagnosticsLog);

        WinRtBluetoothPairingService pairingService = new(diagnosticsLog);
        SelectedDeviceViewModel selectedDevice = new(
            new KernelStreamingBluetoothConnectionService(diagnosticsLog),
            pairingService,
            diagnosticsLog,
            TimeProvider.System);
        PairingViewModel pairing = new(pairingService, uiScheduler, diagnosticsLog, TimeProvider.System);

        SettingsViewModel settings = new(
            settingsStore,
            diagnosticsLog,
            new AssemblyApplicationInfo(typeof(App).Assembly),
            new ShellExternalLinkOpener(),
            activeLanguageTag);
        DisplayModeViewModel displayMode = new();
        SharedNoiseControlService noiseControlService = new(
            new OppoNoiseControlService(diagnosticsLog, TimeProvider.System));

        _mainViewModel = new MainViewModel(
            devices,
            selectedDevice,
            settings,
            pairing,
            allOutputs,
            deviceOutputs,
            mediaTransport,
            new NoiseControlViewModel(noiseControlService, settingsStore, uiScheduler, diagnosticsLog),
            new DiagnosticsViewModel(diagnosticsLog, uiScheduler),
            displayMode);
        _widgetViewModel = new WidgetViewModel(
            devices,
            selectedDevice,
            mediaTransport,
            new NoiseControlViewModel(noiseControlService, settingsStore, uiScheduler, diagnosticsLog),
            widgetOutputs,
            displayMode,
            settings);
        _settings = settings;
        _displayMode = displayMode;

        _mainWindow = new MainWindow
        {
            Title = AppResources.ApplicationTitle,
            DataContext = _mainViewModel
        };

        _mainWindow.Show();
        displayMode.PropertyChanged += OnDisplayModePropertyChanged;
        displayMode.ExitRequested += OnExitRequested;

        _ = InitializeAsync(devices, allOutputs, diagnosticsLog);
        _ = InitializeMediaTransportAsync(mediaTransport, diagnosticsLog);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;

        if (_displayMode is not null)
        {
            _displayMode.PropertyChanged -= OnDisplayModePropertyChanged;
            _displayMode.ExitRequested -= OnExitRequested;
        }

        if (_widgetWindow is not null)
        {
            _widgetWindow.Closed -= OnWidgetWindowClosed;
            _widgetWindow.Close();
            _widgetWindow = null;
        }

        _widgetViewModel?.Dispose();
        _widgetViewModel = null;
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

    private void OnDisplayModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayModeViewModel.IsWidgetMode))
        {
            ApplyDisplayMode();
        }
    }

    private void ApplyDisplayMode()
    {
        if (_displayMode is null || _widgetViewModel is null || _settings is null || _mainWindow is null)
        {
            return;
        }

        if (!_displayMode.IsWidgetMode)
        {
            ShowMainWindow(_mainWindow);
            _widgetWindow?.Hide();

            return;
        }

        if (_widgetWindow is null)
        {
            _widgetWindow = CreateWidgetWindow(_widgetViewModel, _settings.WidgetPosition);
            _widgetWindow.Closed += OnWidgetWindowClosed;
        }

        _widgetWindow.Show();
        _widgetWindow.Activate();
        _mainWindow.Hide();
    }

    private static void ShowMainWindow(MainWindow window)
    {
        window.Show();

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        ShutDownOnce();
    }

    private void OnWidgetWindowClosed(object? sender, EventArgs e)
    {
        _widgetWindow = null;
        ShutDownOnce();
    }

    private void ShutDownOnce()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Shutdown();
    }

    private WidgetWindow CreateWidgetWindow(WidgetViewModel widget, WidgetPosition? remembered)
    {
        Rect workArea = SystemParameters.WorkArea;
        double widgetWidth = (double)FindResource(WidgetMaxWidthKey);
        WidgetPosition fallback = new(workArea.Right - widgetWidth - WidgetScreenMargin, workArea.Top + WidgetScreenMargin);
        Rect virtualScreen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        WidgetPosition position = WidgetPlacement.Place(remembered, fallback, virtualScreen);

        return new WidgetWindow
        {
            Title = AppResources.ApplicationTitle,
            DataContext = widget,
            Left = position.Left,
            Top = position.Top
        };
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
