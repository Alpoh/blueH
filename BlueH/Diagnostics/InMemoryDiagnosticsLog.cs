namespace BlueH.Diagnostics;

public sealed class InMemoryDiagnosticsLog : IDiagnosticsLog
{
    public const int MaximumEntries = 500;

    private readonly Lock _gate = new();
    private readonly Queue<DiagnosticEntry> _entries = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryDiagnosticsLog(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public event EventHandler<DiagnosticEntry>? EntryRecorded;

    public IReadOnlyList<DiagnosticEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Record(DiagnosticOutcome outcome, string operation, string? detail = null)
    {
        DiagnosticEntry entry = new(_timeProvider.GetUtcNow(), outcome, operation, detail);

        lock (_gate)
        {
            _entries.Enqueue(entry);

            while (_entries.Count > MaximumEntries)
            {
                _entries.Dequeue();
            }
        }

        EntryRecorded?.Invoke(this, entry);
    }
}
