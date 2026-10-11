namespace BlueH.Vendors.Oppo;

public sealed record OppoDecodeResult(IReadOnlyList<OppoFrame> Frames, IReadOnlyList<string> Problems);
