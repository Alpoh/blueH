namespace BlueH.Vendors.Oppo;

public interface IOppoChannel : IAsyncDisposable
{
    event EventHandler<byte[]>? BytesReceived;

    event EventHandler<string>? Closed;

    Task SendAsync(byte[] bytes, CancellationToken cancellationToken);
}
