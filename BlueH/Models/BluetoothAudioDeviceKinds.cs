namespace BlueH.Models;

public static class BluetoothAudioDeviceKinds
{
    private const uint WearableHeadsetMinorClass = 1;
    private const uint HandsFreeMinorClass = 2;
    private const uint LoudspeakerMinorClass = 5;
    private const uint HeadphonesMinorClass = 6;

    public static BluetoothAudioDeviceKind FromMinorClass(uint? minorClass)
    {
        return minorClass switch
        {
            WearableHeadsetMinorClass or HandsFreeMinorClass or HeadphonesMinorClass => BluetoothAudioDeviceKind.Headphones,
            LoudspeakerMinorClass => BluetoothAudioDeviceKind.Loudspeaker,
            _ => BluetoothAudioDeviceKind.Unknown
        };
    }
}
