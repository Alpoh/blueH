namespace BlueH.Vendors.Oppo;

public sealed record OppoFrame(ushort Command, byte Sequence, byte[] Payload)
{
    public const ushort ResponseFlag = 0x8000;

    public bool IsResponseTo(ushort requestCommand)
    {
        return Command == (requestCommand | ResponseFlag);
    }
}
