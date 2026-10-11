namespace BlueH.Models;

public readonly record struct MediaTransportControls(
    bool CanPlay,
    bool CanPause,
    bool CanSkipNext,
    bool CanSkipPrevious);
