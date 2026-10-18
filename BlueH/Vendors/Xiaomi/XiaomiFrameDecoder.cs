using System.Buffers.Binary;
using System.Globalization;

namespace BlueH.Vendors.Xiaomi;

public sealed class XiaomiFrameDecoder
{
    public const int MaximumBodyLength = 2048;

    private const string SkippedBytesFormat = "skipped {0} bytes before a preamble";
    private const string ImplausibleLengthFormat = "implausible body length {0}";
    private const string MissingTrailerFormat = "frame of {0} bytes without its trailer discarded";

    private readonly List<byte> _buffer = [];

    public int BufferedByteCount => _buffer.Count;

    public XiaomiDecodeResult Append(ReadOnlySpan<byte> received)
    {
        _buffer.AddRange(received.ToArray());

        List<XiaomiFrame> frames = [];
        List<string> problems = [];

        while (_buffer.Count > 0)
        {
            int skipped = BytesBeforePossiblePreamble();

            if (skipped > 0)
            {
                _buffer.RemoveRange(0, skipped);
                problems.Add(string.Format(CultureInfo.InvariantCulture, SkippedBytesFormat, skipped));

                continue;
            }

            if (_buffer.Count < XiaomiFrameCodec.HeaderLength)
            {
                break;
            }

            byte[] header = [.. _buffer.GetRange(0, XiaomiFrameCodec.HeaderLength)];
            int bodyLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(XiaomiFrameCodec.LengthOffset));

            if (bodyLength > MaximumBodyLength)
            {
                _buffer.RemoveAt(0);
                problems.Add(string.Format(CultureInfo.InvariantCulture, ImplausibleLengthFormat, bodyLength));

                continue;
            }

            int frameLength = XiaomiFrameCodec.HeaderLength + bodyLength + 1;

            if (_buffer.Count < frameLength)
            {
                break;
            }

            if (_buffer[frameLength - 1] != XiaomiFrameCodec.Trailer)
            {
                _buffer.RemoveAt(0);
                problems.Add(string.Format(CultureInfo.InvariantCulture, MissingTrailerFormat, frameLength));

                continue;
            }

            frames.Add(new XiaomiFrame(
                header[XiaomiFrameCodec.TypeOffset],
                header[XiaomiFrameCodec.OpcodeOffset],
                [.. _buffer.GetRange(XiaomiFrameCodec.HeaderLength, bodyLength)]));
            _buffer.RemoveRange(0, frameLength);
        }

        return new XiaomiDecodeResult(frames, problems);
    }

    private int BytesBeforePossiblePreamble()
    {
        ReadOnlySpan<byte> preamble = XiaomiFrameCodec.Preamble;

        for (int start = 0; start < _buffer.Count; start++)
        {
            int comparable = Math.Min(preamble.Length, _buffer.Count - start);
            bool matches = true;

            for (int index = 0; index < comparable && matches; index++)
            {
                matches = _buffer[start + index] == preamble[index];
            }

            if (matches)
            {
                return start;
            }
        }

        return _buffer.Count;
    }
}
