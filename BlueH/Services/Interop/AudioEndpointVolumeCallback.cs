namespace BlueH.Services.Interop;

internal sealed class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
{
    private const int Handled = 0;

    private readonly Action _onLevelsChanged;

    public AudioEndpointVolumeCallback(Action onLevelsChanged)
    {
        _onLevelsChanged = onLevelsChanged;
    }

    public int OnNotify(IntPtr notificationData)
    {
        _onLevelsChanged();

        return Handled;
    }
}
