using BlueH.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.Diagnostics;

[TestFixture]
public class InMemoryDiagnosticsLogTests
{
    private const string AnyOperation = "Enumerate Bluetooth devices";
    private const string AnyDetail = "3 devices returned";

    private static readonly DateTimeOffset AnyInstant = new(2026, 10, 10, 15, 48, 3, TimeSpan.Zero);

    private FakeTimeProvider _timeProvider = null!;
    private InMemoryDiagnosticsLog _log = null!;

    [SetUp]
    public void SetUp()
    {
        _timeProvider = new FakeTimeProvider(AnyInstant);
        _log = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [Test]
    public void RecordKeepsTheOperation()
    {
        _log.Record(DiagnosticOutcome.Success, AnyOperation, AnyDetail);

        Assert.That(_log.Entries[0].Operation, Is.EqualTo(AnyOperation));
    }

    [Test]
    public void RecordKeepsTheOutcome()
    {
        _log.Record(DiagnosticOutcome.Failure, AnyOperation);

        Assert.That(_log.Entries[0].Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public void RecordLeavesTheDetailAbsentWhenNoneIsGiven()
    {
        _log.Record(DiagnosticOutcome.Information, AnyOperation);

        Assert.That(_log.Entries[0].Detail, Is.Null);
    }

    [Test]
    public void RecordTakesTheTimeFromTheTimeProviderRatherThanTheClock()
    {
        _log.Record(DiagnosticOutcome.Success, AnyOperation);

        Assert.That(_log.Entries[0].RecordedAt, Is.EqualTo(AnyInstant));
    }

    [Test]
    public void RecordAdvancesWithTheTimeProvider()
    {
        _log.Record(DiagnosticOutcome.Success, AnyOperation);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _log.Record(DiagnosticOutcome.Success, AnyOperation);

        Assert.That(_log.Entries[1].RecordedAt, Is.EqualTo(AnyInstant.AddMinutes(5)));
    }

    [Test]
    public void RecordRaisesEntryRecordedWithTheNewEntry()
    {
        List<DiagnosticEntry> raised = [];
        _log.EntryRecorded += (_, entry) => raised.Add(entry);

        _log.Record(DiagnosticOutcome.Success, AnyOperation);

        Assert.That(raised, Is.EqualTo(_log.Entries));
    }

    [Test]
    public void EntriesStayInTheOrderTheyWereRecorded()
    {
        _log.Record(DiagnosticOutcome.Information, "first");
        _log.Record(DiagnosticOutcome.Information, "second");
        _log.Record(DiagnosticOutcome.Information, "third");

        Assert.That(_log.Entries.Select(entry => entry.Operation), Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void EntriesIsASnapshotThatLaterRecordsDoNotChange()
    {
        _log.Record(DiagnosticOutcome.Information, AnyOperation);
        IReadOnlyList<DiagnosticEntry> snapshot = _log.Entries;

        _log.Record(DiagnosticOutcome.Information, AnyOperation);

        Assert.That(snapshot, Has.Count.EqualTo(1));
    }

    [Test]
    public void RecordDropsTheOldestEntryOnceTheLogIsFull()
    {
        for (int index = 0; index <= InMemoryDiagnosticsLog.MaximumEntries; index++)
        {
            _log.Record(DiagnosticOutcome.Information, index.ToString());
        }

        Assert.That(_log.Entries, Has.Count.EqualTo(InMemoryDiagnosticsLog.MaximumEntries));
    }

    [Test]
    public void RecordKeepsTheNewestEntryOnceTheLogIsFull()
    {
        for (int index = 0; index <= InMemoryDiagnosticsLog.MaximumEntries; index++)
        {
            _log.Record(DiagnosticOutcome.Information, index.ToString());
        }

        Assert.That(_log.Entries[^1].Operation, Is.EqualTo(InMemoryDiagnosticsLog.MaximumEntries.ToString()));
    }

    [Test]
    public void RecordDropsTheFirstEntryOnceTheLogIsFull()
    {
        for (int index = 0; index <= InMemoryDiagnosticsLog.MaximumEntries; index++)
        {
            _log.Record(DiagnosticOutcome.Information, index.ToString());
        }

        Assert.That(_log.Entries[0].Operation, Is.EqualTo("1"));
    }

    [Test]
    public void RecordLosesNothingWhenCalledFromManyThreadsAtOnce()
    {
        const int callsPerThread = 50;
        const int threads = 8;

        Parallel.For(0, threads, _ =>
        {
            for (int index = 0; index < callsPerThread; index++)
            {
                _log.Record(DiagnosticOutcome.Information, AnyOperation);
            }
        });

        Assert.That(_log.Entries, Has.Count.EqualTo(threads * callsPerThread));
    }
}
