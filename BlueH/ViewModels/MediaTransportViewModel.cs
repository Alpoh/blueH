using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using BlueH.Themes;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class MediaTransportViewModel : ViewModelBase, IDisposable
{
    private const string ReadSessionOperation = "Read media session";
    private const string SendCommandOperation = "Send media command";
    private const string ReadArtworkOperation = "Read media artwork";

    private readonly IMediaTransportService _mediaTransportService;
    private readonly IUiScheduler _uiScheduler;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly AsyncRelayCommand _playPauseCommand;
    private readonly AsyncRelayCommand _skipNextCommand;
    private readonly AsyncRelayCommand _skipPreviousCommand;

    private MediaSession? _session;
    private MediaTrack? _artworkTrack;
    private byte[]? _artwork;
    private string? _errorDetail;

    public MediaTransportViewModel(
        IMediaTransportService mediaTransportService,
        IUiScheduler uiScheduler,
        IDiagnosticsLog diagnosticsLog)
    {
        _mediaTransportService = mediaTransportService;
        _uiScheduler = uiScheduler;
        _diagnosticsLog = diagnosticsLog;
        _playPauseCommand = new AsyncRelayCommand(_ => PlayOrPauseAsync(), _ => CanPlayOrPause());
        _skipNextCommand = new AsyncRelayCommand(
            _ => SendAsync(MediaTransportAction.SkipNext),
            _ => _session?.Controls.CanSkipNext == true);
        _skipPreviousCommand = new AsyncRelayCommand(
            _ => SendAsync(MediaTransportAction.SkipPrevious),
            _ => _session?.Controls.CanSkipPrevious == true);
        _mediaTransportService.SessionChanged += OnSessionChanged;
    }

    public bool HasSession => _session is not null;

    public bool HasNoSession => _session is null && !HasError;

    public bool HasError => _errorDetail is not null;

    public string? ErrorDetail
    {
        get => _errorDetail;
        private set
        {
            if (SetField(ref _errorDetail, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(HasNoSession));
            }
        }
    }

    public string? SourceAppId => _session?.SourceAppId;

    public string TitleText => _session?.Title ?? UnknownTitleText;

    public string ArtistText => _session?.Artist ?? UnknownArtistText;

    public ICommand PlayPauseCommand => _playPauseCommand;

    public string PlayPauseLabel => IsPlaying ? PauseLabel : PlayLabel;

    public string PlayPauseGlyph => IsPlaying ? Glyphs.Pause : Glyphs.Play;

    public byte[]? Artwork
    {
        get => _artwork;
        private set
        {
            if (SetField(ref _artwork, value))
            {
                OnPropertyChanged(nameof(HasArtwork));
            }
        }
    }

    public bool HasArtwork => _artwork is not null;

    public string ArtworkPlaceholderGlyph => Glyphs.Music;

    public ICommand SkipNextCommand => _skipNextCommand;

    public ICommand SkipPreviousCommand => _skipPreviousCommand;

    public string Header => AppResources.MediaHeader;

    public string NoSessionMessage => AppResources.MediaNoSessionMessage;

    public string ErrorMessage => AppResources.MediaErrorMessage;

    public string UnknownTitleText => AppResources.MediaUnknownTitle;

    public string UnknownArtistText => AppResources.MediaUnknownArtist;

    public string PlayLabel => AppResources.MediaPlayLabel;

    public string PauseLabel => AppResources.MediaPauseLabel;

    public string NextLabel => AppResources.MediaNextLabel;

    public string PreviousLabel => AppResources.MediaPreviousLabel;

    public string CommandRefusedMessage => AppResources.MediaCommandRefusedMessage;

    private bool IsPlaying => _session?.IsPlaying == true;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _mediaTransportService.StartAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            MediaSession? session = await _mediaTransportService.ReadCurrentSessionAsync(cancellationToken);

            _uiScheduler.Post(() =>
            {
                ErrorDetail = null;
                Apply(session);
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            _uiScheduler.Post(() => ErrorDetail = failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ReadSessionOperation, failure.Message);
        }
    }

    public void Dispose()
    {
        _mediaTransportService.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync(CancellationToken.None);
    }

    private void Apply(MediaSession? session)
    {
        if (session == _session)
        {
            return;
        }

        _session = session;
        FollowArtwork(session);
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(HasNoSession));
        OnPropertyChanged(nameof(SourceAppId));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(ArtistText));
        OnPropertyChanged(nameof(PlayPauseLabel));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        _playPauseCommand.RaiseCanExecuteChanged();
        _skipNextCommand.RaiseCanExecuteChanged();
        _skipPreviousCommand.RaiseCanExecuteChanged();
    }

    private void FollowArtwork(MediaSession? session)
    {
        MediaTrack? track = session is null ? null : new MediaTrack(session.SourceAppId, session.Title, session.Artist);

        if (track == _artworkTrack)
        {
            return;
        }

        _artworkTrack = track;
        Artwork = null;

        if (track is { } current)
        {
            _ = LoadArtworkAsync(current);
        }
    }

    private async Task LoadArtworkAsync(MediaTrack track)
    {
        byte[]? artwork;

        try
        {
            artwork = await _mediaTransportService.ReadArtworkAsync(CancellationToken.None);
        }
        catch (Exception failure)
        {
            artwork = null;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ReadArtworkOperation, failure.Message);
        }

        _uiScheduler.Post(() =>
        {
            if (_artworkTrack == track)
            {
                Artwork = artwork;
            }
        });
    }

    private bool CanPlayOrPause()
    {
        if (_session is null)
        {
            return false;
        }

        return _session.IsPlaying ? _session.Controls.CanPause : _session.Controls.CanPlay;
    }

    private Task PlayOrPauseAsync()
    {
        return SendAsync(IsPlaying ? MediaTransportAction.Pause : MediaTransportAction.Play);
    }

    private async Task SendAsync(MediaTransportAction action)
    {
        try
        {
            if (await _mediaTransportService.TrySendAsync(action, CancellationToken.None))
            {
                return;
            }

            ErrorDetail = CommandRefusedMessage;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SendCommandOperation, action.ToString());
        }
        catch (Exception failure)
        {
            ErrorDetail = failure.Message;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SendCommandOperation, failure.Message);
        }
    }
}
