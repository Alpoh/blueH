using System.Buffers.Binary;

namespace BlueH.Vendors.Oppo;

public static class OppoFrameCodec
{
    public const byte Preamble = 0xAA;
    public const int HeaderLength = 7;

    private const int ReservedLength = 2;
    private const int CommandOffset = 2;
    private const int SequenceOffset = 4;
    private const int PayloadLengthOffset = 5;

    public static byte[] Encode(OppoFrame frame)
    {
        byte[] length = Leb128.Encode((uint)(HeaderLength + frame.Payload.Length));
        byte[] encoded = new byte[1 + length.Length + HeaderLength + frame.Payload.Length];
        Span<byte> packet = encoded.AsSpan(1 + length.Length);

        encoded[0] = Preamble;
        length.CopyTo(encoded, 1);
        packet[..ReservedLength].Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(packet[CommandOffset..], frame.Command);
        packet[SequenceOffset] = frame.Sequence;
        BinaryPrimitives.WriteUInt16LittleEndian(packet[PayloadLengthOffset..], (ushort)frame.Payload.Length);
        frame.Payload.CopyTo(packet[HeaderLength..]);

        return encoded;
    }

    public static OppoFrame? DecodePacket(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < HeaderLength)
        {
            return null;
        }

        ushort payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(packet[PayloadLengthOffset..]);

        if (payloadLength != packet.Length - HeaderLength)
        {
            return null;
        }

        return new OppoFrame(
            BinaryPrimitives.ReadUInt16LittleEndian(packet[CommandOffset..]),
            packet[SequenceOffset],
            packet[HeaderLength..].ToArray());
    }
}
