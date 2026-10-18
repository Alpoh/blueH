namespace BlueH.Vendors.Xiaomi;

public sealed record XiaomiDecodeResult(IReadOnlyList<XiaomiFrame> Frames, IReadOnlyList<string> Problems);
