namespace BlueH.Services;

public interface IAudioEndpointChangeWatcher : IDisposable
{
    event EventHandler? Changed;

    void Start();

    void Stop();
}
