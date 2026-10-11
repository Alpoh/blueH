namespace BlueH.Models;

public sealed record MediaSession(
    string SourceAppId,
    string? Title,
    string? Artist,
    bool IsPlaying,
    MediaTransportControls Controls);
