using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeAudioEndpointChangeWatcher : IAudioEndpointChangeWatcher
{
    public event EventHandler? Changed;

    public bool IsStarted { get; private set; }

    public bool IsStopped { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool HasSubscribers => Changed is not null;

    public void Start()
    {
        IsStarted = true;
    }

    public void Stop()
    {
        IsStopped = true;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
