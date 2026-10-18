namespace BlueH.Vendors;

public interface IVendorChannel : IAsyncDisposable
{
    event EventHandler<byte[]>? BytesReceived;

    event EventHandler<string>? Closed;

    Task SendAsync(byte[] bytes, CancellationToken cancellationToken);
}
