using System.Globalization;
using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;

namespace BlueH.Vendors.Oppo;

public sealed class OppoNoiseControlSession : INoiseControlSession
{
    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(3);

    private const string ReadModeOperation = "Read noise control mode";
    private const string SetModeOperation = "Set noise control mode";
    private const string ModeReportedOperation = "Noise control mode reported";
    private const string DecodeOperation = "Decode OPPO frame";
    private const string ChannelClosedOperation = "OPPO channel closed";
    private const string UnknownCodeFormat = "unknown mode in response {0}";
    private const string RefusedFormat = "the device refused {0} with status {1}";
    private const string TimedOutFormat = "no response to command 0x{0:X4} within {1} ms";
    private const string ClosedDetail = "the OPPO channel is closed";

    private readonly IOppoChannel _channel;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly TimeProvider _timeProvider;
    private readonly OppoFrameDecoder _decoder = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<(ushort Command, byte Sequence), TaskCompletionSource<OppoFrame>> _awaitedResponses = [];

    private byte _nextSequence;
    private bool _isClosed;

    public OppoNoiseControlSession(IOppoChannel channel, IDiagnosticsLog diagnosticsLog, TimeProvider timeProvider)
    {
        _channel = channel;
        _diagnosticsLog = diagnosticsLog;
        _timeProvider = timeProvider;
        _channel.BytesReceived += OnBytesReceived;
        _channel.Closed += OnChannelClosed;
    }

    public event EventHandler<NoiseControlMode>? ModeReported;

    public event EventHandler? Closed;

    public async Task<NoiseControlMode?> ReadModeAsync(CancellationToken cancellationToken)
    {
        OppoFrame response = await ExchangeAsync(ReadModeOperation, OppoNoiseControlMessages.ModeRequest, cancellationToken);
        NoiseControlMode? mode = OppoNoiseControlMessages.DecodeModeResponse(response.Payload);

        if (mode is { } known)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Success, ReadModeOperation, known.ToString());
        }
        else
        {
            _diagnosticsLog.Record(
                DiagnosticOutcome.Failure,
                ReadModeOperation,
                string.Format(CultureInfo.InvariantCulture, UnknownCodeFormat, Hex(response.Payload)));
        }

        return mode;
    }

    public async Task SetModeAsync(NoiseControlMode mode, CancellationToken cancellationToken)
    {
        OppoFrame acknowledgement = await ExchangeAsync(
            SetModeOperation,
            sequence => OppoNoiseControlMessages.SetMode(mode, sequence),
            cancellationToken);

        if (!OppoNoiseControlMessages.IsSuccessfulAcknowledgement(acknowledgement.Payload))
        {
            string detail = string.Format(CultureInfo.InvariantCulture, RefusedFormat, mode, Hex(acknowledgement.Payload));
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SetModeOperation, detail);

            throw new InvalidOperationException(detail);
        }

        _diagnosticsLog.Record(DiagnosticOutcome.Success, SetModeOperation, mode.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        _channel.BytesReceived -= OnBytesReceived;
        _channel.Closed -= OnChannelClosed;
        FailAwaitedResponses();
        await _channel.DisposeAsync();
    }

    private async Task<OppoFrame> ExchangeAsync(
        string operation,
        Func<byte, OppoFrame> createRequest,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<OppoFrame> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        OppoFrame request;
        (ushort Command, byte Sequence) key;

        lock (_gate)
        {
            if (_isClosed)
            {
                throw new IOException(ClosedDetail);
            }

            request = createRequest(_nextSequence++);
            key = ((ushort)(request.Command | OppoFrame.ResponseFlag), request.Sequence);
            _awaitedResponses[key] = response;
        }

        using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await _channel.SendAsync(OppoFrameCodec.Encode(request), cancellationToken);

            Task timeout = Task.Delay(ResponseTimeout, _timeProvider, timeoutCancellation.Token);
            Task first = await Task.WhenAny(response.Task, timeout);

            cancellationToken.ThrowIfCancellationRequested();

            if (first != response.Task)
            {
                string detail = string.Format(
                    CultureInfo.InvariantCulture,
                    TimedOutFormat,
                    request.Command,
                    ResponseTimeout.TotalMilliseconds);
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, detail);

                throw new TimeoutException(detail);
            }

            return await response.Task;
        }
        finally
        {
            await timeoutCancellation.CancelAsync();

            lock (_gate)
            {
                _awaitedResponses.Remove(key);
            }
        }
    }

    private void OnBytesReceived(object? sender, byte[] received)
    {
        OppoDecodeResult result = _decoder.Append(received);

        foreach (string problem in result.Problems)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, DecodeOperation, problem);
        }

        foreach (OppoFrame frame in result.Frames)
        {
            Dispatch(frame);
        }
    }

    private void Dispatch(OppoFrame frame)
    {
        TaskCompletionSource<OppoFrame>? awaited;

        lock (_gate)
        {
            _awaitedResponses.TryGetValue((frame.Command, frame.Sequence), out awaited);
        }

        if (awaited is not null)
        {
            awaited.TrySetResult(frame);

            return;
        }

        if (frame.Command == (ushort)OppoCommand.DeviceNotification
            && OppoNoiseControlMessages.DecodeModeNotification(frame.Payload) is { } mode)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ModeReportedOperation, mode.ToString());
            ModeReported?.Invoke(this, mode);
        }
    }

    private void OnChannelClosed(object? sender, string reason)
    {
        _diagnosticsLog.Record(DiagnosticOutcome.Failure, ChannelClosedOperation, reason);
        FailAwaitedResponses();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void FailAwaitedResponses()
    {
        TaskCompletionSource<OppoFrame>[] awaited;

        lock (_gate)
        {
            _isClosed = true;
            awaited = [.. _awaitedResponses.Values];
            _awaitedResponses.Clear();
        }

        foreach (TaskCompletionSource<OppoFrame> response in awaited)
        {
            response.TrySetException(new IOException(ClosedDetail));
        }
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
