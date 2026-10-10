namespace BlueH.Diagnostics;

public interface IDiagnosticsLog
{
    event EventHandler<DiagnosticEntry>? EntryRecorded;

    IReadOnlyList<DiagnosticEntry> Entries { get; }

    void Record(DiagnosticOutcome outcome, string operation, string? detail = null);
}
