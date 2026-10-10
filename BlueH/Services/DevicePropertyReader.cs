namespace BlueH.Services;

public static class DevicePropertyReader
{
    public const string ContainerIdProperty = "System.Devices.Aep.ContainerId";
    public const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    public const string MinorClassProperty = "System.Devices.Aep.Bluetooth.Cod.Minor";

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
        return Read(properties, MinorClassProperty) switch
        {
            ushort minorClass => minorClass,
            uint minorClass => minorClass,
            byte minorClass => minorClass,
            short minorClass when minorClass >= 0 => (uint)minorClass,
            int minorClass when minorClass >= 0 => (uint)minorClass,
            _ => null
        };
    }

    private static object? Read(IReadOnlyDictionary<string, object?> properties, string key)
    {
        return properties.TryGetValue(key, out object? value) ? value : null;
    }
}
