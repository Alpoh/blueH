using System.Windows;
using BlueH.Diagnostics;
using AppResources = BlueH.Properties.Resources;
using BlueH.Services;
using BlueH.ViewModels;

namespace BlueH;

public partial class App
{
    private const string LoadSettingsOperation = "Load settings";
    private const string StartUpOperation = "Start up";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InMemoryDiagnosticsLog diagnosticsLog = new(TimeProvider.System);
        DispatcherUiScheduler uiScheduler = new(Dispatcher);
        JsonSettingsStore settingsStore = new(JsonSettingsStore.DefaultFilePath);

        diagnosticsLog.Record(DiagnosticOutcome.Information, StartUpOperation, JsonSettingsStore.DefaultFilePath);
        RecordSettingsLoad(diagnosticsLog, settingsStore.Load());

        MainWindow window = new()
        {
            Title = AppResources.ApplicationTitle,
            DataContext = new DiagnosticsViewModel(diagnosticsLog, uiScheduler)
        };

        window.Show();
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
