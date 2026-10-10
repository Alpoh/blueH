using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class EndpointsViewModelTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid HeadsetContainer = Guid.Parse("4969f11a-c4a9-11f1-8cd8-acf23ce11594");

    private const string GameEndpointId = "{0.0.0.00000000}.{63a7089b-a6c3-4900-b04a-d95f0654486c}";
    private const string ChatEndpointId = "{0.0.0.00000000}.{3b555345-216d-4e1e-8459-383ebd89af67}";
    private const string GameEndpointName = "Headphones (INZONE H5 - Game)";
    private const string ChatEndpointName = "Headset Earphone (INZONE H5 - Chat)";

    private FakeAudioEndpointService _endpointService = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;

    [SetUp]
    public void SetUp()
    {
        _endpointService = new FakeAudioEndpointService();
        _uiScheduler = new RecordingUiScheduler();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
    }

    [Test]
    public async Task RefreshListsTheEndpoints()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        _endpointService.Endpoints.Add(Endpoint(ChatEndpointId, HeadsetContainer, ChatEndpointName, 100));
        EndpointsViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(
            viewModel.Endpoints.Select(endpoint => endpoint.Name),
            Is.EqualTo(new[] { GameEndpointName, ChatEndpointName }));
    }

    [Test]
    public async Task TwoEndpointsOfOneDeviceKeepTheirOwnVolumes()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        _endpointService.Endpoints.Add(Endpoint(ChatEndpointId, HeadsetContainer, ChatEndpointName, 100));
        EndpointsViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(
            viewModel.Endpoints.Select(endpoint => endpoint.VolumePercent),
            Is.EqualTo(new[] { 76, 100 }));
    }

    [Test]
    public async Task TheContainerFilterKeepsOnlyTheSelectedDevicesEndpoints()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        _endpointService.Endpoints.Add(Endpoint(ChatEndpointId, EarbudsContainer, "Headphones (OPPO Enco Air5)", 50));
        EndpointsViewModel viewModel = CreateViewModel();
        viewModel.ContainerFilter = EarbudsContainer;

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Endpoints.Select(endpoint => endpoint.MmDeviceId), Is.EqualTo(new[] { ChatEndpointId }));
    }

    [Test]
    public async Task ADeviceWithNoEndpointReportsHavingNone()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        EndpointsViewModel viewModel = CreateViewModel();
        viewModel.ContainerFilter = EarbudsContainer;

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.HasNoEndpoints, Is.True);
    }

    [Test]
    public async Task AFailedRefreshIsSurfacedAndRecorded()
    {
        _endpointService.FailureToThrow = new InvalidOperationException("core audio said no");
        EndpointsViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.ErrorDetail, Is.EqualTo("core audio said no"));
    }

    [Test]
    public async Task AFailedRefreshIsNotMistakenForHavingNoEndpoints()
    {
        _endpointService.FailureToThrow = new InvalidOperationException("core audio said no");
        EndpointsViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.HasNoEndpoints, Is.False);
    }

    [Test]
    public async Task ARefreshUpdatesTheExistingRowRatherThanReplacingIt()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        EndpointsViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();
        AudioEndpointViewModel original = viewModel.Endpoints[0];

        _endpointService.Endpoints[0] = Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 30);
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Endpoints[0], Is.SameAs(original));
    }

    [Test]
    public async Task ARefreshBringsTheNewVolumeIntoTheExistingRow()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        EndpointsViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        _endpointService.Endpoints[0] = Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 30);
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Endpoints[0].VolumePercent, Is.EqualTo(30));
    }

    [Test]
    public async Task AnEndpointThatDisappearsLeavesTheList()
    {
        _endpointService.Endpoints.Add(Endpoint(GameEndpointId, HeadsetContainer, GameEndpointName, 76));
        EndpointsViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        _endpointService.Endpoints.Clear();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Endpoints, Is.Empty);
    }

    [Test]
    public async Task SettingTheVolumeReachesTheService()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);

        endpoint.VolumePercent = 42;

        Assert.That(_endpointService.VolumeCalls, Is.EqualTo(new[] { (GameEndpointId, 42) }));
    }

    [Test]
    public async Task DraggingTheSliderCollapsesIntoOneCallPerCompletedWrite()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);
        _endpointService.BlockUntilReleased();

        endpoint.VolumePercent = 10;
        endpoint.VolumePercent = 20;
        endpoint.VolumePercent = 30;
        int callsWhileTheFirstWasRunning = _endpointService.VolumeCalls.Count;
        _endpointService.Release();

        Assert.That(callsWhileTheFirstWasRunning, Is.EqualTo(1));
    }

    [Test]
    public async Task TheLastVolumeTheUserChoseIsTheOneThatSticks()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);
        _endpointService.BlockUntilReleased();

        endpoint.VolumePercent = 10;
        endpoint.VolumePercent = 20;
        endpoint.VolumePercent = 30;
        _endpointService.Release();
        await Task.Delay(50);

        Assert.That(_endpointService.VolumeCalls[^1].VolumePercent, Is.EqualTo(30));
    }

    [Test]
    public async Task TogglingMuteAsksForTheOppositeOfTheCurrentState()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76, isMuted: false);

        await ((AsyncRelayCommand)endpoint.ToggleMuteCommand).ExecuteAsync(null);

        Assert.That(_endpointService.MuteCalls, Is.EqualTo(new[] { (GameEndpointId, true) }));
    }

    [Test]
    public async Task TogglingMuteOnAMutedEndpointUnmutesIt()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76, isMuted: true);

        await ((AsyncRelayCommand)endpoint.ToggleMuteCommand).ExecuteAsync(null);

        Assert.That(_endpointService.MuteCalls, Is.EqualTo(new[] { (GameEndpointId, false) }));
    }

    [Test]
    public async Task AnEndpointWithUnknownMuteStateIsMutedFirst()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76, isMuted: null);

        await ((AsyncRelayCommand)endpoint.ToggleMuteCommand).ExecuteAsync(null);

        Assert.That(_endpointService.MuteCalls, Is.EqualTo(new[] { (GameEndpointId, true) }));
    }

    [Test]
    public async Task AFailedMuteIsRecordedRatherThanThrown()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);
        _endpointService.FailureToThrow = new InvalidOperationException("the endpoint vanished");

        await ((AsyncRelayCommand)endpoint.ToggleMuteCommand).ExecuteAsync(null);

        Assert.That(
            _diagnosticsLog.Entries.Select(entry => entry.Detail),
            Does.Contain("the endpoint vanished"));
    }

    [Test]
    public async Task MakingAnEndpointDefaultForPlaybackAsksForThatRole()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);

        await ((AsyncRelayCommand)endpoint.MakeDefaultForPlaybackCommand).ExecuteAsync(null);

        Assert.That(_endpointService.DefaultCalls, Is.EqualTo(new[] { (GameEndpointId, AudioEndpointRole.Playback) }));
    }

    [Test]
    public async Task MakingAnEndpointDefaultForCallsAsksForTheOtherRole()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);

        await ((AsyncRelayCommand)endpoint.MakeDefaultForCommunicationsCommand).ExecuteAsync(null);

        Assert.That(
            _endpointService.DefaultCalls,
            Is.EqualTo(new[] { (GameEndpointId, AudioEndpointRole.Communications) }));
    }

    [Test]
    public async Task AnEndpointThatIsAlreadyTheDefaultCannotBeMadeDefaultAgain()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76, isDefaultForPlayback: true);

        Assert.That(endpoint.MakeDefaultForPlaybackCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task AnEndpointThatIsNotTheDefaultCanBeMadeDefault()
    {
        AudioEndpointViewModel endpoint = await SingleEndpointAsync(76);

        Assert.That(endpoint.MakeDefaultForPlaybackCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task TheDefaultRolesAreReadIndependently()
    {
        _endpointService.Endpoints.Add(Endpoint(
            GameEndpointId,
            HeadsetContainer,
            GameEndpointName,
            76,
            isDefaultForPlayback: true,
            isDefaultForCommunications: false));
        EndpointsViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(
            new[] { viewModel.Endpoints[0].IsDefaultForPlayback, viewModel.Endpoints[0].IsDefaultForCommunications },
            Is.EqualTo(new[] { true, false }));
    }

    private async Task<AudioEndpointViewModel> SingleEndpointAsync(
        int volumePercent,
        bool? isMuted = false,
        bool isDefaultForPlayback = false)
    {
        _endpointService.Endpoints.Add(Endpoint(
            GameEndpointId,
            HeadsetContainer,
            GameEndpointName,
            volumePercent,
            isMuted,
            isDefaultForPlayback));

        EndpointsViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();
        _endpointService.VolumeCalls.Clear();

        return viewModel.Endpoints[0];
    }

    private EndpointsViewModel CreateViewModel()
    {
        return new EndpointsViewModel(_endpointService, _uiScheduler, _diagnosticsLog);
    }

    private static AudioEndpoint Endpoint(
        string mmDeviceId,
        Guid containerId,
        string name,
        int? volumePercent,
        bool? isMuted = false,
        bool isDefaultForPlayback = false,
        bool isDefaultForCommunications = false)
    {
        return new AudioEndpoint(
            mmDeviceId,
            containerId,
            name,
            volumePercent,
            isMuted,
            isDefaultForPlayback,
            isDefaultForCommunications);
    }
}
