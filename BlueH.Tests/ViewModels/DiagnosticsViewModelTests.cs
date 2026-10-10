using BlueH.Diagnostics;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class DiagnosticsViewModelTests
{
    private const string AnyOperation = "Enumerate Bluetooth devices";

    private InMemoryDiagnosticsLog _log = null!;
    private RecordingUiScheduler _uiScheduler = null!;

    [SetUp]
    public void SetUp()
    {
        _log = new InMemoryDiagnosticsLog(new FakeTimeProvider());
        _uiScheduler = new RecordingUiScheduler();
    }

    [Test]
    public void TheViewModelShowsEntriesRecordedBeforeItExisted()
    {
        _log.Record(DiagnosticOutcome.Information, AnyOperation);

        using DiagnosticsViewModel viewModel = new(_log, _uiScheduler);

        Assert.That(viewModel.Entries.Select(entry => entry.Operation), Is.EqualTo(new[] { AnyOperation }));
    }

    [Test]
    public void TheViewModelAppendsEntriesRecordedWhileItIsAlive()
    {
        using DiagnosticsViewModel viewModel = new(_log, _uiScheduler);

        _log.Record(DiagnosticOutcome.Success, AnyOperation);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Entries.Select(entry => entry.Operation), Is.EqualTo(new[] { AnyOperation }));
    }

    [Test]
    public void TheViewModelNeverTouchesItsCollectionOffTheUiThread()
    {
        using DiagnosticsViewModel viewModel = new(_log, _uiScheduler);

        _log.Record(DiagnosticOutcome.Success, AnyOperation);

        Assert.That(viewModel.Entries, Is.Empty);
    }

    [Test]
    public void TheViewModelKeepsEntriesInTheOrderTheyWereRecorded()
    {
        using DiagnosticsViewModel viewModel = new(_log, _uiScheduler);

        _log.Record(DiagnosticOutcome.Information, "first");
        _log.Record(DiagnosticOutcome.Information, "second");
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Entries.Select(entry => entry.Operation), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void DisposingTheViewModelStopsItListening()
    {
        DiagnosticsViewModel viewModel = new(_log, _uiScheduler);
        viewModel.Dispose();

        _log.Record(DiagnosticOutcome.Success, AnyOperation);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Entries, Is.Empty);
    }

    [Test]
    public void TheHeadersComeFromResourcesRatherThanFromLiterals()
    {
        using DiagnosticsViewModel viewModel = new(_log, _uiScheduler);

        Assert.That(
            new[] { viewModel.TimeHeader, viewModel.OutcomeHeader, viewModel.OperationHeader, viewModel.DetailHeader },
            Has.All.Not.Null.And.All.Not.Empty);
    }

}
