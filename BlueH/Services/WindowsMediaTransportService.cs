using System.Globalization;
using BlueH.Diagnostics;
using BlueH.Models;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace BlueH.Services;

public sealed class WindowsMediaTransportService : IMediaTransportService
{
    private const string StartOperation = "Start media session manager";
    private const string ReadSessionOperation = "Read media session";
    private const string SendCommandOperation = "Send media command";
    private const string NoSessionDetail = "no current session";
    private const string ReadArtworkOperation = "Read media artwork";
    private const ulong MaximumArtworkBytes = 4 * 1024 * 1024;

    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly Lock _gate = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _observedSession;

    public WindowsMediaTransportService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public event EventHandler? SessionChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSessionManager manager =
            await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);

        lock (_gate)
        {
            _manager = manager;
        }

        manager.CurrentSessionChanged += OnCurrentSessionChanged;
        ObserveCurrentSession();

        _diagnosticsLog.Record(DiagnosticOutcome.Success, StartOperation);
    }

    public async Task<MediaSession?> ReadCurrentSessionAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSession? session = CurrentSession();

        if (session is null)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ReadSessionOperation, NoSessionDetail);

            return null;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties properties =
            await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = session.GetPlaybackInfo();
        GlobalSystemMediaTransportControlsSessionPlaybackControls controls = playbackInfo.Controls;

        MediaSession mediaSession = MediaSessionMapper.Map(
            session.SourceAppUserModelId,
            properties.Title,
            properties.Artist,
            playbackInfo.PlaybackStatus,
            new MediaTransportControls(
                controls.IsPlayEnabled,
                controls.IsPauseEnabled,
                controls.IsNextEnabled,
                controls.IsPreviousEnabled));

        _diagnosticsLog.Record(
            DiagnosticOutcome.Success,
            ReadSessionOperation,
            $"{mediaSession.SourceAppId} {playbackInfo.PlaybackStatus} {mediaSession.Controls}");

        return mediaSession;
    }

    public async Task<bool> TrySendAsync(MediaTransportAction action, CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSession? session = CurrentSession();

        if (session is null)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SendCommandOperation, $"{action} {NoSessionDetail}");

            return false;
        }

        bool accepted = await Send(session, action).AsTask(cancellationToken);

        _diagnosticsLog.Record(
            accepted ? DiagnosticOutcome.Success : DiagnosticOutcome.Failure,
            SendCommandOperation,
            $"{action} {session.SourceAppUserModelId} accepted={accepted}");

        return accepted;
    }

    public async Task<byte[]?> ReadArtworkAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSession? session = CurrentSession();

        if (session is null)
        {
            return null;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties properties =
            await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);

        if (properties.Thumbnail is null)
        {
            return null;
        }

        using IRandomAccessStreamWithContentType stream =
            await properties.Thumbnail.OpenReadAsync().AsTask(cancellationToken);

        if (stream.Size is 0 or > MaximumArtworkBytes)
        {
            _diagnosticsLog.Record(
                DiagnosticOutcome.Failure,
                ReadArtworkOperation,
                string.Create(CultureInfo.InvariantCulture, $"{stream.ContentType} size={stream.Size}"));

            return null;
        }

        using DataReader reader = new(stream);
        uint loaded = await reader.LoadAsync((uint)stream.Size).AsTask(cancellationToken);
        byte[] artwork = new byte[loaded];
        reader.ReadBytes(artwork);

        _diagnosticsLog.Record(
            DiagnosticOutcome.Success,
            ReadArtworkOperation,
            string.Create(CultureInfo.InvariantCulture, $"{stream.ContentType} {artwork.Length} bytes"));

        return artwork;
    }

    public void Dispose()
    {
        GlobalSystemMediaTransportControlsSessionManager? manager;

        lock (_gate)
        {
            manager = _manager;
            _manager = null;
        }

        if (manager is not null)
        {
            manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        Observe(null);
    }

    private static IAsyncOperation<bool> Send(
        GlobalSystemMediaTransportControlsSession session,
        MediaTransportAction action)
    {
        return action switch
        {
            MediaTransportAction.Play => session.TryPlayAsync(),
            MediaTransportAction.Pause => session.TryPauseAsync(),
            MediaTransportAction.SkipNext => session.TrySkipNextAsync(),
            MediaTransportAction.SkipPrevious => session.TrySkipPreviousAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    private GlobalSystemMediaTransportControlsSession? CurrentSession()
    {
        lock (_gate)
        {
            return _manager?.GetCurrentSession();
        }
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        ObserveCurrentSession();
        RaiseSessionChanged();
    }

    private void ObserveCurrentSession()
    {
        Observe(CurrentSession());
    }

    private void Observe(GlobalSystemMediaTransportControlsSession? session)
    {
        GlobalSystemMediaTransportControlsSession? previous;

        lock (_gate)
        {
            previous = _observedSession;
            _observedSession = session;
        }

        if (previous is not null)
        {
            previous.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            previous.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        }

        if (session is not null)
        {
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        }
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        RaiseSessionChanged();
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        RaiseSessionChanged();
    }

    private void RaiseSessionChanged()
    {
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
