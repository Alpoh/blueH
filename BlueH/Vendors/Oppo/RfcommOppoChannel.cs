using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BlueH.Vendors.Oppo;

public sealed class RfcommOppoChannel : IOppoChannel
{
    private const uint ReadBufferLength = 1024;
    private const string EndOfStreamDetail = "the remote device closed the connection";

    private readonly RfcommDeviceService _service;
    private readonly StreamSocket _socket;
    private readonly DataWriter _writer;
    private readonly DataReader _reader;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    private int _isDisposed;

    public RfcommOppoChannel(RfcommDeviceService service, StreamSocket socket)
    {
        _service = service;
        _socket = socket;
        _writer = new DataWriter(socket.OutputStream);
        _reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
    }

    public event EventHandler<byte[]>? BytesReceived;

    public event EventHandler<string>? Closed;

    public void StartReading()
    {
        _ = ReadUntilClosedAsync();
    }

    public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
            _writer.WriteBytes(bytes);
            await _writer.StoreAsync();
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _socket.Dispose();
            _service.Dispose();
            _sendGate.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task ReadUntilClosedAsync()
    {
        string reason = EndOfStreamDetail;

        try
        {
            while (true)
            {
                uint count = await _reader.LoadAsync(ReadBufferLength);

                if (count == 0)
                {
                    break;
                }

                byte[] received = new byte[count];
                _reader.ReadBytes(received);
                BytesReceived?.Invoke(this, received);
            }
        }
        catch (Exception failure)
        {
            reason = failure.Message;
        }

        if (Volatile.Read(ref _isDisposed) == 0)
        {
            Closed?.Invoke(this, reason);
        }
    }
}
