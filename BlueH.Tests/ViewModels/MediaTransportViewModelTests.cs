using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class MediaTransportViewModelTests
{
    private const string EdgeAppId = "MSEdge";
    private const string SpotifyAppId = "Spotify.exe";
    private const string Title = "The Mundane and the Magic";
    private const string OtherTitle = "Therein";
    private const string Artist = "Dark Tranquillity";
    private const string ReadFailure = "session manager unavailable";
    private const string CommandFailure = "session closed mid-command";

    private static readonly MediaTransportControls PlayingControls = new(
        CanPlay: false,
        CanPause: true,
        CanSkipNext: true,
        CanSkipPrevious: true);

    private static readonly MediaTransportControls PausedControls = PlayingControls with
    {
        CanPlay = true,
        CanPause = false
    };

    private FakeMediaTransportService _mediaService = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaService = new FakeMediaTransportService();
        _uiScheduler = new RecordingUiScheduler();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
    }

    [TearDown]
    public void TearDown()
    {
        _mediaService.Dispose();
    }

    [Test]
    public async Task StartingStartsTheServiceAndShowsTheCurrentTrack()
    {
        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        using MediaTransportViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(_mediaService.IsStarted, Is.True);
            Assert.That(viewModel.HasSession, Is.True);
            Assert.That(viewModel.HasNoSession, Is.False);
            Assert.That(viewModel.SourceAppId, Is.EqualTo(EdgeAppId));
            Assert.That(viewModel.TitleText, Is.EqualTo(Title));
            Assert.That(viewModel.ArtistText, Is.EqualTo(Artist));
        });
    }

    [Test]
    public async Task NoSessionIsReportedAsNothingPlaying()
    {
        using MediaTransportViewModel viewModel = CreateViewModel();

        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasSession, Is.False);
            Assert.That(viewModel.HasNoSession, Is.True);
            Assert.That(viewModel.HasError, Is.False);
        });
    }

    [Test]
    public async Task ASessionWithEmptyMetadataShowsPlaceholdersRatherThanBlanks()
    {
        _mediaService.Session = Session(EdgeAppId, title: null, artist: null);
        using MediaTransportViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasSession, Is.True);
            Assert.That(viewModel.TitleText, Is.EqualTo(viewModel.UnknownTitleText).And.Not.Empty);
            Assert.That(viewModel.ArtistText, Is.EqualTo(viewModel.UnknownArtistText).And.Not.Empty);
        });
    }

    [Test]
    public async Task ASessionChangeRereadsTheTrack()
    {
        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        using MediaTransportViewModel viewModel = CreateViewModel();
        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        _mediaService.Session = Session(SpotifyAppId, OtherTitle, Artist);
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SourceAppId, Is.EqualTo(SpotifyAppId));
            Assert.That(viewModel.TitleText, Is.EqualTo(OtherTitle));
        });
    }

    [Test]
    public async Task TheSessionEndingIsReportedAsNothingPlaying()
    {
        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        using MediaTransportViewModel viewModel = CreateViewModel();
        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        _mediaService.Session = null;
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasSession, Is.False);
            Assert.That(viewModel.HasNoSession, Is.True);
        });
    }

    [Test]
    public async Task ARepeatedIdenticalChangeRaisesNoPropertyChange()
    {
        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        using MediaTransportViewModel viewModel = CreateViewModel();
        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.That(changedProperties, Is.Empty);
    }

    [Test]
    public async Task AFailedReadIsShownAndLogged()
    {
        _mediaService.FailureToThrow = new InvalidOperationException(ReadFailure);
        using MediaTransportViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.HasNoSession, Is.False);
            Assert.That(viewModel.ErrorDetail, Is.EqualTo(ReadFailure));
            Assert.That(
                _diagnosticsLog.Entries.Any(entry => entry.Outcome == DiagnosticOutcome.Failure),
                Is.True);
        });
    }

    [Test]
    public async Task ASuccessfulReadAfterAFailureClearsTheError()
    {
        _mediaService.FailureToThrow = new InvalidOperationException(ReadFailure);
        using MediaTransportViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        _mediaService.FailureToThrow = null;
        _mediaService.Session = Session(EdgeAppId, Title, Artist);
        await viewModel.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.HasSession, Is.True);
        });
    }

    [Test]
    public void DisposingUnsubscribesFromTheService()
    {
        MediaTransportViewModel viewModel = CreateViewModel();
        Assert.That(_mediaService.HasSubscribers, Is.True);

        viewModel.Dispose();

        Assert.That(_mediaService.HasSubscribers, Is.False);
    }

    [Test]
    public async Task ThePlayPauseGlyphFollowsThePlaybackState()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();
        string glyphWhilePlaying = viewModel.PlayPauseGlyph;
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _mediaService.Session = Paused(PausedControls);
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(glyphWhilePlaying, Is.EqualTo(BlueH.Themes.Glyphs.Pause));
            Assert.That(viewModel.PlayPauseGlyph, Is.EqualTo(BlueH.Themes.Glyphs.Play));
            Assert.That(changed, Does.Contain(nameof(MediaTransportViewModel.PlayPauseGlyph)));
        });
    }

    [Test]
    public async Task WhilePlayingTheButtonOffersPause()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await PlayPause(viewModel).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PlayPauseLabel, Is.EqualTo(viewModel.PauseLabel).And.Not.Empty);
            Assert.That(_mediaService.SentActions, Is.EqualTo(new[] { MediaTransportAction.Pause }));
        });
    }

    [Test]
    public async Task WhilePausedTheButtonOffersPlay()
    {
        _mediaService.Session = Paused(PausedControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await PlayPause(viewModel).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PlayPauseLabel, Is.EqualTo(viewModel.PlayLabel).And.Not.Empty);
            Assert.That(_mediaService.SentActions, Is.EqualTo(new[] { MediaTransportAction.Play }));
        });
    }

    [Test]
    public async Task PlayPauseIsDisabledWhenTheSessionForbidsPausingWhatIsPlaying()
    {
        _mediaService.Session = Playing(PlayingControls with { CanPlay = true, CanPause = false });
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.That(viewModel.PlayPauseCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task PlayPauseIsDisabledWhenTheSessionForbidsPlayingWhatIsPaused()
    {
        _mediaService.Session = Paused(PausedControls with { CanPlay = false, CanPause = true });
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.That(viewModel.PlayPauseCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task PlayPauseIsDisabledWithNoSession()
    {
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.That(viewModel.PlayPauseCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task APlaybackChangeFlipsTheButtonAndAnnouncesIt()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();
        List<string?> changedProperties = [];
        int canExecuteChanges = 0;
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        viewModel.PlayPauseCommand.CanExecuteChanged += (_, _) => canExecuteChanges++;

        _mediaService.Session = Paused(PausedControls);
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PlayPauseLabel, Is.EqualTo(viewModel.PlayLabel));
            Assert.That(changedProperties, Does.Contain(nameof(MediaTransportViewModel.PlayPauseLabel)));
            Assert.That(canExecuteChanges, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task ARefusedCommandIsShownAndLogged()
    {
        _mediaService.Session = Playing(PlayingControls);
        _mediaService.AcceptsCommands = false;
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await PlayPause(viewModel).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorDetail, Is.EqualTo(viewModel.CommandRefusedMessage).And.Not.Empty);
            Assert.That(
                _diagnosticsLog.Entries.Any(entry => entry.Outcome == DiagnosticOutcome.Failure),
                Is.True);
        });
    }

    [Test]
    public async Task AFailingCommandIsShownAndLogged()
    {
        _mediaService.Session = Playing(PlayingControls);
        _mediaService.CommandFailureToThrow = new InvalidOperationException(CommandFailure);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await PlayPause(viewModel).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorDetail, Is.EqualTo(CommandFailure));
            Assert.That(
                _diagnosticsLog.Entries.Any(entry =>
                    entry.Outcome == DiagnosticOutcome.Failure && entry.Detail == CommandFailure),
                Is.True);
        });
    }

    [Test]
    public async Task AnAcceptedCommandReportsNoError()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await PlayPause(viewModel).ExecuteAsync(null);

        Assert.That(viewModel.HasError, Is.False);
    }

    [Test]
    public async Task PlayPauseIsNotReentrant()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();
        _mediaService.BlockUntilReleased();

        Task first = PlayPause(viewModel).ExecuteAsync(null);
        Task second = PlayPause(viewModel).ExecuteAsync(null);
        bool disabledWhileRunning = !viewModel.PlayPauseCommand.CanExecute(null);
        _mediaService.Release();
        await Task.WhenAll(first, second);

        Assert.Multiple(() =>
        {
            Assert.That(disabledWhileRunning, Is.True);
            Assert.That(_mediaService.SentActions, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task NextSkipsForward()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await ((AsyncRelayCommand)viewModel.SkipNextCommand).ExecuteAsync(null);

        Assert.That(_mediaService.SentActions, Is.EqualTo(new[] { MediaTransportAction.SkipNext }));
    }

    [Test]
    public async Task PreviousSkipsBack()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await ((AsyncRelayCommand)viewModel.SkipPreviousCommand).ExecuteAsync(null);

        Assert.That(_mediaService.SentActions, Is.EqualTo(new[] { MediaTransportAction.SkipPrevious }));
    }

    [Test]
    public async Task SkippingIsEnabledOnlyWhereTheSessionAllowsIt()
    {
        _mediaService.Session = Playing(PlayingControls with { CanSkipNext = true, CanSkipPrevious = false });
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SkipNextCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.SkipPreviousCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task SkippingForwardCanBeForbiddenWhileSkippingBackIsAllowed()
    {
        _mediaService.Session = Playing(PlayingControls with { CanSkipNext = false, CanSkipPrevious = true });
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SkipNextCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.SkipPreviousCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task SkippingIsDisabledWithNoSession()
    {
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SkipNextCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.SkipPreviousCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task ASessionChangeAnnouncesThatSkippingMayHaveChanged()
    {
        _mediaService.Session = Playing(PlayingControls);
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();
        int nextChanges = 0;
        int previousChanges = 0;
        viewModel.SkipNextCommand.CanExecuteChanged += (_, _) => nextChanges++;
        viewModel.SkipPreviousCommand.CanExecuteChanged += (_, _) => previousChanges++;

        _mediaService.Session = Playing(PlayingControls with { CanSkipNext = false, CanSkipPrevious = false });
        _mediaService.RaiseSessionChanged();
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(nextChanges, Is.GreaterThan(0));
            Assert.That(previousChanges, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task ARefusedSkipIsShown()
    {
        _mediaService.Session = Playing(PlayingControls);
        _mediaService.AcceptsCommands = false;
        using MediaTransportViewModel viewModel = await StartedViewModelAsync();

        await ((AsyncRelayCommand)viewModel.SkipNextCommand).ExecuteAsync(null);

        Assert.That(viewModel.ErrorDetail, Is.EqualTo(viewModel.CommandRefusedMessage));
    }

    private static AsyncRelayCommand PlayPause(MediaTransportViewModel viewModel)
    {
        return (AsyncRelayCommand)viewModel.PlayPauseCommand;
    }

    private static MediaSession Playing(MediaTransportControls controls)
    {
        return new MediaSession(EdgeAppId, Title, Artist, IsPlaying: true, controls);
    }

    private static MediaSession Paused(MediaTransportControls controls)
    {
        return new MediaSession(EdgeAppId, Title, Artist, IsPlaying: false, controls);
    }

    private async Task<MediaTransportViewModel> StartedViewModelAsync()
    {
        MediaTransportViewModel viewModel = CreateViewModel();
        await viewModel.StartAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        return viewModel;
    }

    private static MediaSession Session(string sourceAppId, string? title, string? artist)
    {
        return new MediaSession(sourceAppId, title, artist, IsPlaying: true, PlayingControls);
    }

    private MediaTransportViewModel CreateViewModel()
    {
        return new MediaTransportViewModel(_mediaService, _uiScheduler, _diagnosticsLog);
    }
}
