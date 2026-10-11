namespace BlueH.Services.Interop;

internal static class CoreAudioConstants
{
    public const uint ClassContextAll = 0x17;
    public const uint DeviceStateActive = 0x1;
    public const uint DeviceStateMaskAll = 0xF;
    public const uint StorageAccessRead = 0x0;

    public static Guid DeviceEnumeratorClassId { get; } = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    public static Guid PolicyConfigClassId { get; } = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
}
