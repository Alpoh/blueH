namespace BlueH.Vendors.Oppo;

public enum OppoCommand : ushort
{
    NoiseControlRequest = 0x010C,
    DeviceNotification = 0x0204,
    NoiseControlSet = 0x0404,
    NoiseControlAcknowledgement = 0x8404,
    NoiseControlResponse = 0x810C
}
