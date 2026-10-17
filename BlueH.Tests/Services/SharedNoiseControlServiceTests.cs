using System.IO;
using BlueH.Models;
using BlueH.Services;
using BlueH.Tests.Fakes;

namespace BlueH.Tests.Services;

[TestFixture]
public class SharedNoiseControlServiceTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private FakeNoiseControlService _inner = null!;
    private FakeNoiseControlSession _earbuds = null!;
    private SharedNoiseControlService _shared = null!;

    [SetUp]
    public void SetUp()
    {
        _inner = new FakeNoiseControlService();
        _earbuds = new FakeNoiseControlSession { ModeToReport = NoiseControlMode.Transparency };
        _inner.Sessions[EarbudsContainer] = _earbuds;
        _shared = new SharedNoiseControlService(_inner);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _earbuds.DisposeAsync();
    }

    [Test]
    public async Task TwoUsersOfOneDeviceShareOneConnection()
    {
        await using INoiseControlSession? first = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(_inner.OpenCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task EachDeviceHasItsOwnConnection()
    {
        _inner.Sessions[SpeakerContainer] = new FakeNoiseControlSession();
        await using INoiseControlSession? earbuds = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? speaker = await _shared.OpenAsync(SpeakerContainer, CancellationToken.None);

        Assert.That(_inner.OpenCalls, Is.EqualTo(new[] { EarbudsContainer, SpeakerContainer }));
    }

    [Test]
    public async Task TheConnectionStaysOpenUntilTheLastUserLeaves()
    {
        INoiseControlSession first = (await _shared.OpenAsync(EarbudsContainer, CancellationToken.None))!;
        INoiseControlSession second = (await _shared.OpenAsync(EarbudsContainer, CancellationToken.None))!;

        await first.DisposeAsync();
        bool closedAfterFirst = _earbuds.IsDisposed;
        await second.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(closedAfterFirst, Is.False);
            Assert.That(_earbuds.IsDisposed, Is.True);
        });
    }

    [Test]
    public async Task DisposingAUserTwiceDoesNotCloseTheOthersConnection()
    {
        INoiseControlSession first = (await _shared.OpenAsync(EarbudsContainer, CancellationToken.None))!;
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.That(_earbuds.IsDisposed, Is.False);
    }

    [Test]
    public async Task OpeningAgainAfterEveryoneLeftOpensAFreshConnection()
    {
        INoiseControlSession first = (await _shared.OpenAsync(EarbudsContainer, CancellationToken.None))!;
        await first.DisposeAsync();

        await using INoiseControlSession? again = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        Assert.That(_inner.OpenCalls, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ADeviceWithoutNoiseControlIsUnsupportedForEveryone()
    {
        INoiseControlSession? first = await _shared.OpenAsync(SpeakerContainer, CancellationToken.None);
        INoiseControlSession? second = await _shared.OpenAsync(SpeakerContainer, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Null);
        });
    }

    [Test]
    public async Task AFailedOpenIsNotReusedByTheNextUser()
    {
        _inner.OpenFailureToThrow = new IOException("refused");
        Assert.ThrowsAsync<IOException>(() => _shared.OpenAsync(EarbudsContainer, CancellationToken.None));
        _inner.OpenFailureToThrow = null;

        await using INoiseControlSession? retried = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        Assert.That(retried, Is.Not.Null);
    }

    [Test]
    public async Task OneUserGivingUpWhileOpeningLeavesTheConnectionToTheOthers()
    {
        _inner.PendingOpen = new TaskCompletionSource<INoiseControlSession?>();
        using CancellationTokenSource givingUp = new();
        Task<INoiseControlSession?> first = _shared.OpenAsync(EarbudsContainer, givingUp.Token);
        Task<INoiseControlSession?> second = _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        await givingUp.CancelAsync();
        Assert.That(() => first, Throws.InstanceOf<OperationCanceledException>());
        _inner.PendingOpen.SetResult(_earbuds);
        await using INoiseControlSession? waited = await second;
        await using INoiseControlSession? third = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(waited, Is.Not.Null);
            Assert.That(_inner.OpenCalls, Has.Count.EqualTo(1));
            Assert.That(_earbuds.IsDisposed, Is.False);
        });
    }

    [Test]
    public async Task ReadsAndChangesGoToTheSharedConnection()
    {
        await using INoiseControlSession? user = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        NoiseControlMode? mode = await user!.ReadModeAsync(CancellationToken.None);
        await user.SetModeAsync(NoiseControlMode.Off, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(mode, Is.EqualTo(NoiseControlMode.Transparency));
            Assert.That(_earbuds.SetCalls, Is.EqualTo(new[] { NoiseControlMode.Off }));
        });
    }

    [Test]
    public async Task AModeReportedByTheDeviceReachesEveryUser()
    {
        await using INoiseControlSession? first = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        List<NoiseControlMode> firstHeard = [];
        List<NoiseControlMode> secondHeard = [];
        first!.ModeReported += (_, mode) => firstHeard.Add(mode);
        second!.ModeReported += (_, mode) => secondHeard.Add(mode);

        _earbuds.Report(NoiseControlMode.Off);

        Assert.Multiple(() =>
        {
            Assert.That(firstHeard, Is.EqualTo(new[] { NoiseControlMode.Off }));
            Assert.That(secondHeard, Is.EqualTo(new[] { NoiseControlMode.Off }));
        });
    }

    [Test]
    public async Task AChangeMadeByOneUserIsReportedToTheOthers()
    {
        await using INoiseControlSession? first = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        List<NoiseControlMode> secondHeard = [];
        second!.ModeReported += (_, mode) => secondHeard.Add(mode);

        await first!.SetModeAsync(NoiseControlMode.NoiseCancelling, CancellationToken.None);

        Assert.That(secondHeard, Is.EqualTo(new[] { NoiseControlMode.NoiseCancelling }));
    }

    [Test]
    public async Task AFailedChangeIsReportedToNobody()
    {
        _earbuds.SetFailureToThrow = new InvalidOperationException("refused");
        await using INoiseControlSession? first = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        List<NoiseControlMode> secondHeard = [];
        second!.ModeReported += (_, mode) => secondHeard.Add(mode);

        Assert.ThrowsAsync<InvalidOperationException>(() => first!.SetModeAsync(NoiseControlMode.Off, CancellationToken.None));

        Assert.That(secondHeard, Is.Empty);
    }

    [Test]
    public async Task ALostConnectionClosesEveryUserAndIsNotReused()
    {
        await using INoiseControlSession? first = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        int closed = 0;
        first!.Closed += (_, _) => closed++;
        second!.Closed += (_, _) => closed++;

        _earbuds.Close();
        await using INoiseControlSession? reopened = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.EqualTo(2));
            Assert.That(_inner.OpenCalls, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task AUserWhoLeftHearsNothingMore()
    {
        INoiseControlSession first = (await _shared.OpenAsync(EarbudsContainer, CancellationToken.None))!;
        await using INoiseControlSession? second = await _shared.OpenAsync(EarbudsContainer, CancellationToken.None);
        List<NoiseControlMode> firstHeard = [];
        first.ModeReported += (_, mode) => firstHeard.Add(mode);
        await first.DisposeAsync();

        _earbuds.Report(NoiseControlMode.Off);

        Assert.That(firstHeard, Is.Empty);
    }
}
