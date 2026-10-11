namespace BlueH.Models;

public sealed record PairingResponse(bool IsAccepted, string? Pin)
{
    public static PairingResponse Rejected { get; } = new(IsAccepted: false, Pin: null);

    public static PairingResponse Accept(string? pin = null)
    {
        return new PairingResponse(IsAccepted: true, pin);
    }
}
