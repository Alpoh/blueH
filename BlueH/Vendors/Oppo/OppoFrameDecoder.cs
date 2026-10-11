using System.Globalization;

namespace BlueH.Vendors.Oppo;

public sealed class OppoFrameDecoder
{
    public const int MaximumPacketLength = 2048;

    private const string SkippedBytesFormat = "skipped {0} bytes before a preamble";
    private const string MalformedLengthDetail = "malformed length prefix";
    private const string ImplausibleLengthFormat = "implausible packet length {0}";
    private const string InconsistentPacketFormat = "inconsistent packet of {0} bytes discarded";

    private readonly List<byte> _buffer = [];

    public int BufferedByteCount => _buffer.Count;

    public OppoDecodeResult Append(ReadOnlySpan<byte> received)
    {
        _buffer.AddRange(received.ToArray());

        List<OppoFrame> frames = [];
        List<string> problems = [];

        while (_buffer.Count > 0)
        {
            int preamble = _buffer.IndexOf(OppoFrameCodec.Preamble);

            if (preamble != 0)
            {
                int skipped = preamble < 0 ? _buffer.Count : preamble;
                _buffer.RemoveRange(0, skipped);
                problems.Add(string.Format(CultureInfo.InvariantCulture, SkippedBytesFormat, skipped));

                continue;
            }

            Leb128Read length = Leb128.TryDecode(_buffer, 1);

            if (length.Status == Leb128ReadStatus.Incomplete)
            {
                break;
            }

            if (length.Status == Leb128ReadStatus.Malformed)
            {
                _buffer.RemoveAt(0);
                problems.Add(MalformedLengthDetail);

                continue;
            }

            if (length.Value > MaximumPacketLength)
            {
                _buffer.RemoveAt(0);
                problems.Add(string.Format(CultureInfo.InvariantCulture, ImplausibleLengthFormat, length.Value));

                continue;
            }

            int packetStart = 1 + length.EncodedLength;
            int packetLength = (int)length.Value;

            if (_buffer.Count < packetStart + packetLength)
            {
                break;
            }

            byte[] packet = [.. _buffer.GetRange(packetStart, packetLength)];
            _buffer.RemoveRange(0, packetStart + packetLength);

            if (OppoFrameCodec.DecodePacket(packet) is { } frame)
            {
                frames.Add(frame);
            }
            else
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture, InconsistentPacketFormat, packetLength));
            }
        }

        return new OppoDecodeResult(frames, problems);
    }
}
