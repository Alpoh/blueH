using BlueH.Time;

namespace BlueH.Diagnostics;

public sealed record DiagnosticEntry(
    DateTimeOffset RecordedAt,
    DiagnosticOutcome Outcome,
    string Operation,
    string? Detail)
{
    public string RecordedAtText => CanonicalTimestamp.Format(RecordedAt);
}
