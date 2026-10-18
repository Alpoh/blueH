using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.Vendors.Oppo;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.Vendors.Oppo;

[TestFixture]
public class OppoNoiseControlSessionTests
{
    private const string ChannelLostReason = "the remote device closed the connection";

    private static readonly byte[] TransparencyResponseToSequenceOne = Convert.FromHexString("aa0b00000c8101040000010104");
    private static readonly byte[] NoiseCancellingNotification = Convert.FromHexString("aa0b0000040200040003010110");
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(200);

    private static readonly byte[] BatteryNotification = Convert.FromHexString("aa0f000004020008000103015002500300");

    private FakeVendorChannel _channel = null!;
    private FakeTimeProvider _timeProvider = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;

    [SetUp]
    public void SetUp()
    {
        _channel = new FakeVendorChannel();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _channel.DisposeAsync();
    }

    [Test]
    public void ReadingTheModeSendsAModeRequest()
    {
        OppoNoiseControlSession session = CreateSession();

        _ = session.ReadModeAsync(CancellationToken.None);

        Assert.That(_channel.Sent, Has.Count.EqualTo(1));
        Assert.That(_channel.Sent[0], Is.EqualTo(OppoFrameCodec.Encode(OppoNoiseControlMessages.ModeRequest(sequence: 0))));
    }

    [Test]
    public async Task TheModeComesFromTheResponseWithTheRequestsSequence()
    {
        OppoNoiseControlSession session = CreateSession();
        await CompleteOneReadAsync(session);

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Receive(TransparencyResponseToSequenceOne);

        Assert.That(await Within(read), Is.EqualTo(NoiseControlMode.Transparency));
    }

    [Test]
    public async Task AResponseSplitAcrossTwoReadsIsStillMatched()
    {
        OppoNoiseControlSession session = CreateSession();
        await CompleteOneReadAsync(session);

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Receive(TransparencyResponseToSequenceOne[..5]);
        _channel.Receive(TransparencyResponseToSequenceOne[5..]);

        Assert.That(await Within(read), Is.EqualTo(NoiseControlMode.Transparency));
    }

    [Test]
    public async Task AResponseToAnotherSequenceDoesNotAnswerTheRequest()
    {
        OppoNoiseControlSession session = CreateSession();

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Receive(TransparencyResponseToSequenceOne);

        Assert.That(await IsStillWaitingAsync(read), Is.True);
    }

    [Test]
    public void ARequestWithNoResponseTimesOutAndIsLogged()
    {
        OppoNoiseControlSession session = CreateSession();

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _timeProvider.Advance(OppoNoiseControlSession.ResponseTimeout);

        Assert.ThrowsAsync<TimeoutException>(() => Within(read));
        Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public async Task ARequestIsStillWaitingJustBeforeTheTimeout()
    {
        OppoNoiseControlSession session = CreateSession();

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _timeProvider.Advance(OppoNoiseControlSession.ResponseTimeout - TimeSpan.FromMilliseconds(1));

        Assert.That(await IsStillWaitingAsync(read), Is.True);
    }

    [Test]
    public async Task AnUnknownReportedCodeReadsAsUnknownAndIsLoggedAsAFailure()
    {
        OppoNoiseControlSession session = CreateSession();

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Receive(Response(0x810C, sequence: 0, [0x00, 0x01, 0x01, 0x08]));

        Assert.That(await Within(read), Is.Null);
        Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public async Task SettingAModeSendsItsCodeAndSucceedsOnAZeroStatus()
    {
        OppoNoiseControlSession session = CreateSession();

        Task set = session.SetModeAsync(NoiseControlMode.NoiseCancelling, CancellationToken.None);
        _channel.Receive(Response(0x8404, sequence: 0, [0x00]));
        await Within(set);

        Assert.That(
            _channel.Sent[0],
            Is.EqualTo(OppoFrameCodec.Encode(OppoNoiseControlMessages.SetMode(NoiseControlMode.NoiseCancelling, sequence: 0))));
        Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Success));
    }

    [Test]
    public void ASetTheDeviceRefusesFailsAndIsLogged()
    {
        OppoNoiseControlSession session = CreateSession();

        Task set = session.SetModeAsync(NoiseControlMode.Off, CancellationToken.None);
        _channel.Receive(Response(0x8404, sequence: 0, [0x01]));

        Assert.ThrowsAsync<InvalidOperationException>(() => Within(set));
        Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public void ANotificationFromTheDeviceReportsItsMode()
    {
        OppoNoiseControlSession session = CreateSession();
        List<NoiseControlMode> reported = [];
        session.ModeReported += (_, mode) => reported.Add(mode);

        _channel.Receive(NoiseCancellingNotification);

        Assert.That(reported, Is.EqualTo(new[] { NoiseControlMode.NoiseCancelling }));
    }

    [Test]
    public void ABatteryNotificationReportsNoMode()
    {
        OppoNoiseControlSession session = CreateSession();
        List<NoiseControlMode> reported = [];
        session.ModeReported += (_, mode) => reported.Add(mode);

        _channel.Receive(BatteryNotification);

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public async Task ANotificationIsNotMistakenForAResponse()
    {
        OppoNoiseControlSession session = CreateSession();

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Receive(NoiseCancellingNotification);

        Assert.That(await IsStillWaitingAsync(read), Is.True);
    }

    [Test]
    public void EachRequestTakesTheNextSequence()
    {
        OppoNoiseControlSession session = CreateSession();

        _ = session.ReadModeAsync(CancellationToken.None);
        _ = session.ReadModeAsync(CancellationToken.None);

        Assert.That(_channel.Sent[1], Is.EqualTo(OppoFrameCodec.Encode(OppoNoiseControlMessages.ModeRequest(sequence: 1))));
    }

    [Test]
    public void LosingTheChannelFailsTheWaitingRequestAndAnnouncesTheClose()
    {
        OppoNoiseControlSession session = CreateSession();
        int closedCount = 0;
        session.Closed += (_, _) => closedCount++;

        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        _channel.Close(ChannelLostReason);

        Assert.ThrowsAsync<IOException>(() => Within(read));
        Assert.That(closedCount, Is.EqualTo(1));
    }

    [Test]
    public void AfterTheChannelIsLostNothingMoreIsSent()
    {
        OppoNoiseControlSession session = CreateSession();
        _channel.Close(ChannelLostReason);

        Assert.ThrowsAsync<IOException>(() => Within(session.ReadModeAsync(CancellationToken.None)));
        Assert.That(_channel.Sent, Is.Empty);
    }

    [Test]
    public void ASendFailureReachesTheCaller()
    {
        OppoNoiseControlSession session = CreateSession();
        _channel.SendFailureToThrow = new IOException(ChannelLostReason);

        Assert.ThrowsAsync<IOException>(() => Within(session.ReadModeAsync(CancellationToken.None)));
    }

    [Test]
    public void CancellingStopsTheWait()
    {
        OppoNoiseControlSession session = CreateSession();
        using CancellationTokenSource cancellation = new();

        Task<NoiseControlMode?> read = session.ReadModeAsync(cancellation.Token);
        cancellation.Cancel();

        Assert.That(() => Within(read), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task DisposingTheSessionDisposesTheChannelWithoutAnnouncingAClose()
    {
        OppoNoiseControlSession session = CreateSession();
        int closedCount = 0;
        session.Closed += (_, _) => closedCount++;

        await session.DisposeAsync();
        _channel.Close(ChannelLostReason);

        Assert.That(_channel.IsDisposed, Is.True);
        Assert.That(closedCount, Is.Zero);
    }

    private OppoNoiseControlSession CreateSession()
    {
        return new OppoNoiseControlSession(_channel, _diagnosticsLog, _timeProvider);
    }

    private async Task CompleteOneReadAsync(OppoNoiseControlSession session)
    {
        Task<NoiseControlMode?> read = session.ReadModeAsync(CancellationToken.None);
        byte sequence = (byte)(_channel.Sent.Count - 1);
        _channel.Receive(Response(0x810C, sequence, [0x00, 0x01, 0x01, 0x01]));
        await Within(read);
    }

    private static Task Within(Task task)
    {
        return task.WaitAsync(Deadline);
    }

    private static Task<T> Within<T>(Task<T> task)
    {
        return task.WaitAsync(Deadline);
    }

    private static async Task<bool> IsStillWaitingAsync(Task task)
    {
        return await Task.WhenAny(task, Task.Delay(SettleTime)) != task;
    }

    private static byte[] Response(ushort command, byte sequence, byte[] payload)
    {
        return OppoFrameCodec.Encode(new OppoFrame(command, sequence, payload));
    }
}
