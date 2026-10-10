using System.Collections.ObjectModel;
using BlueH.Diagnostics;
using AppResources = BlueH.Properties.Resources;
using BlueH.Services;

namespace BlueH.ViewModels;

public sealed class DiagnosticsViewModel : ViewModelBase, IDisposable
{
    private readonly IDiagnosticsLog _log;
    private readonly IUiScheduler _uiScheduler;

    public DiagnosticsViewModel(IDiagnosticsLog log, IUiScheduler uiScheduler)
    {
        _log = log;
        _uiScheduler = uiScheduler;
        Entries = new ObservableCollection<DiagnosticEntry>(log.Entries);
        _log.EntryRecorded += OnEntryRecorded;
    }

    public ObservableCollection<DiagnosticEntry> Entries { get; }

    public string TimeHeader => AppResources.DiagnosticsTimeHeader;

    public string OutcomeHeader => AppResources.DiagnosticsOutcomeHeader;

    public string OperationHeader => AppResources.DiagnosticsOperationHeader;

    public string DetailHeader => AppResources.DiagnosticsDetailHeader;

    public void Dispose()
    {
        _log.EntryRecorded -= OnEntryRecorded;
    }

    private void OnEntryRecorded(object? sender, DiagnosticEntry entry)
    {
        _uiScheduler.Post(() => Entries.Add(entry));
    }
}
