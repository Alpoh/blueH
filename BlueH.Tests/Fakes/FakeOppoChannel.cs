using BlueH.Vendors.Oppo;

namespace BlueH.Tests.Fakes;

public sealed class FakeOppoChannel : IOppoChannel
{
    public List<byte[]> Sent { get; } = [];

    public Exception? SendFailureToThrow { get; set; }

    public bool IsDisposed { get; private set; }

    public event EventHandler<byte[]>? BytesReceived;

    public event EventHandler<string>? Closed;

    public Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (SendFailureToThrow is not null)
        {
            return Task.FromException(SendFailureToThrow);
        }

        Sent.Add(bytes);

        return Task.CompletedTask;
    }

    public void Receive(byte[] bytes)
    {
        BytesReceived?.Invoke(this, bytes);
    }

    public void Close(string reason)
    {
        Closed?.Invoke(this, reason);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;

        return ValueTask.CompletedTask;
    }
}
