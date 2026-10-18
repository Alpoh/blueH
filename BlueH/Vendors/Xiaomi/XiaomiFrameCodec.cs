using System.Buffers.Binary;

namespace BlueH.Vendors.Xiaomi;

public static class XiaomiFrameCodec
{
    public const byte Trailer = 0xEF;
    public const int HeaderLength = 7;
    public const int TypeOffset = 3;
    public const int OpcodeOffset = 4;
    public const int LengthOffset = 5;

    public static ReadOnlySpan<byte> Preamble => [0xFE, 0xDC, 0xBA];

    public static byte[] Encode(XiaomiFrame frame)
    {
        byte[] encoded = new byte[HeaderLength + frame.Body.Length + 1];

        Preamble.CopyTo(encoded);
        encoded[TypeOffset] = frame.Type;
        encoded[OpcodeOffset] = frame.Opcode;
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(LengthOffset), (ushort)frame.Body.Length);
        frame.Body.CopyTo(encoded, HeaderLength);
        encoded[^1] = Trailer;

        return encoded;
    }
}
