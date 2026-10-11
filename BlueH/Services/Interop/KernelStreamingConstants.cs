namespace BlueH.Services.Interop;

internal static class KernelStreamingConstants
{
    public const uint PropertyTypeGet = 0x1;
    public const uint PropertyTypeBasicSupport = 0x200;
    public const uint BasicSupportDataLength = sizeof(uint);
    public const uint NoDataLength = 0;
    public const uint OneShotReconnectProperty = 0;
    public const uint OneShotDisconnectProperty = 1;
    public const ushort VariantTypeClassId = 72;
    public const uint ContainerIdPropertyId = 2;

    public static Guid BluetoothAudioPropertySet { get; } = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

    public static Guid DeviceContainerIdFormat { get; } = new("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C");
}
