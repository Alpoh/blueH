namespace BlueH.Services;

public static class BatteryPropertyScanner
{
    public static int? FindRawPercent(IEnumerable<IReadOnlyDictionary<string, object?>> propertySets)
    {
        foreach (IReadOnlyDictionary<string, object?> properties in propertySets)
        {
            int? rawPercent = DevicePropertyReader.ReadBatteryPercent(properties);

            if (rawPercent.HasValue)
            {
                return rawPercent;
            }
        }

        return null;
    }
}
