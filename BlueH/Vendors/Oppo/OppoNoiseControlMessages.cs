using BlueH.Models;

namespace BlueH.Vendors.Oppo;

public static class OppoNoiseControlMessages
{
    private const byte ModeSetting = 0x01;
    private const byte SettingMarker = 0x01;
    private const byte SuccessStatus = 0x00;
    private const byte OffCode = 0x01;
    private const byte TransparencyCode = 0x04;
    private const byte NoiseCancellingSetCode = 0x02;
    private const byte NoiseCancellingReportedCode = 0x10;
    private const byte ModeNotificationType = 0x03;
    private const int NotificationHeaderLength = 2;
    private const int NotificationEntryLength = 2;

    public static OppoFrame ModeRequest(byte sequence)
    {
        return new OppoFrame((ushort)OppoCommand.NoiseControlRequest, sequence, [ModeSetting, SettingMarker]);
    }

    public static OppoFrame SetMode(NoiseControlMode mode, byte sequence)
    {
        return new OppoFrame((ushort)OppoCommand.NoiseControlSet, sequence, [ModeSetting, SettingMarker, SetCodeOf(mode)]);
    }

    public static NoiseControlMode? DecodeModeResponse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 3 or > 4 || payload[0] != SuccessStatus || payload[1] != ModeSetting)
        {
            return null;
        }

        return ModeOfReportedCode(payload[^1]);
    }

    public static NoiseControlMode? DecodeModeNotification(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < NotificationHeaderLength
            || payload[0] != ModeNotificationType
            || payload.Length != NotificationHeaderLength + (payload[1] * NotificationEntryLength))
        {
            return null;
        }

        for (int entry = NotificationHeaderLength; entry < payload.Length; entry += NotificationEntryLength)
        {
            if (payload[entry] == ModeSetting)
            {
                return ModeOfReportedCode(payload[entry + 1]);
            }
        }

        return null;
    }

    public static bool IsSuccessfulAcknowledgement(ReadOnlySpan<byte> payload)
    {
        return payload.Length > 0 && payload[0] == SuccessStatus;
    }

    private static byte SetCodeOf(NoiseControlMode mode)
    {
        return mode switch
        {
            NoiseControlMode.Off => OffCode,
            NoiseControlMode.Transparency => TransparencyCode,
            NoiseControlMode.NoiseCancelling => NoiseCancellingSetCode,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static NoiseControlMode? ModeOfReportedCode(byte code)
    {
        return code switch
        {
            OffCode => NoiseControlMode.Off,
            TransparencyCode => NoiseControlMode.Transparency,
            NoiseCancellingReportedCode => NoiseControlMode.NoiseCancelling,
            _ => null
        };
    }
}
