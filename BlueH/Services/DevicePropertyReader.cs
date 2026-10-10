namespace BlueH.Services;

public static class DevicePropertyReader
{
    public const string ContainerIdProperty = "System.Devices.Aep.ContainerId";
    public const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    public const string MinorClassProperty = "System.Devices.Aep.Bluetooth.Cod.Minor";
    public const string BatteryPercentProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    public static Guid ReadContainerId(IReadOnlyDictionary<string, object?> properties)
    {
        return Read(properties, ContainerIdProperty) is Guid containerId ? containerId : Guid.Empty;
    }

    public static bool? ReadConnectionState(IReadOnlyDictionary<string, object?> properties)
    {
        return Read(properties, IsConnectedProperty) is bool isConnected ? isConnected : null;
    }

    public static uint? ReadMinorClass(IReadOnlyDictionary<string, object?> properties)
    {
        return ReadNonNegativeInteger(properties, MinorClassProperty) is int minorClass ? (uint)minorClass : null;
    }

    public static int? ReadBatteryPercent(IReadOnlyDictionary<string, object?> properties)
    {
        return ReadNonNegativeInteger(properties, BatteryPercentProperty);
    }

    private static int? ReadNonNegativeInteger(IReadOnlyDictionary<string, object?> properties, string key)
    {
        return Read(properties, key) switch
        {
            byte value => value,
            ushort value => value,
            uint value when value <= int.MaxValue => (int)value,
            sbyte value when value >= 0 => value,
            short value when value >= 0 => value,
            int value when value >= 0 => value,
            long value when value is >= 0 and <= int.MaxValue => (int)value,
            _ => null
        };
    }

    private static object? Read(IReadOnlyDictionary<string, object?> properties, string key)
    {
        return properties.TryGetValue(key, out object? value) ? value : null;
    }
}
