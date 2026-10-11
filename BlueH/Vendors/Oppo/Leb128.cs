namespace BlueH.Vendors.Oppo;

public static class Leb128
{
    public const int MaximumEncodedLength = 5;

    private const int PayloadBits = 7;
    private const byte PayloadMask = 0x7F;
    private const byte ContinuationFlag = 0x80;

    public static byte[] Encode(uint value)
    {
        List<byte> encoded = [];

        do
        {
            byte next = (byte)(value & PayloadMask);
            value >>= PayloadBits;
            encoded.Add(value == 0 ? next : (byte)(next | ContinuationFlag));
        }
        while (value != 0);

        return [.. encoded];
    }

    public static Leb128Read TryDecode(IReadOnlyList<byte> bytes, int start)
    {
        uint value = 0;

        for (int index = 0; index < MaximumEncodedLength; index++)
        {
            if (start + index >= bytes.Count)
            {
                return Leb128Read.Incomplete;
            }

            byte current = bytes[start + index];
            value |= (uint)(current & PayloadMask) << (PayloadBits * index);

            if ((current & ContinuationFlag) == 0)
            {
                return new Leb128Read(Leb128ReadStatus.Complete, value, index + 1);
            }
        }

        return Leb128Read.Malformed;
    }
}
