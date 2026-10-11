namespace BlueH.Vendors.Oppo;

public readonly record struct Leb128Read(Leb128ReadStatus Status, uint Value, int EncodedLength)
{
    public static Leb128Read Incomplete { get; } = new(Leb128ReadStatus.Incomplete, 0, 0);

    public static Leb128Read Malformed { get; } = new(Leb128ReadStatus.Malformed, 0, 0);
}
