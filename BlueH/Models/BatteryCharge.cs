namespace BlueH.Models;

public readonly record struct BatteryCharge
{
    private const int MinimumPercent = 0;
    private const int MaximumPercent = 100;

    private BatteryCharge(int? chargePercent)
    {
        ChargePercent = chargePercent;
    }

    public static BatteryCharge Unknown => new(chargePercent: null);

    public int? ChargePercent { get; }

    public bool IsKnown => ChargePercent.HasValue;

    public static BatteryCharge FromPercent(int chargePercent)
    {
        return chargePercent is >= MinimumPercent and <= MaximumPercent
            ? new BatteryCharge(chargePercent)
            : Unknown;
    }
}
