using BlueH.Models;
using Windows.Media.Control;

namespace BlueH.Services;

public static class MediaSessionMapper
{
    public static MediaSession Map(
        string sourceAppId,
        string? title,
        string? artist,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus playbackStatus,
        MediaTransportControls controls)
    {
        return new MediaSession(
            sourceAppId,
            NullIfBlank(title),
            NullIfBlank(artist),
            playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            controls);
    }

    private static string? NullIfBlank(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
