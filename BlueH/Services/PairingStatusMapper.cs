using BlueH.Models;
using Windows.Devices.Enumeration;

namespace BlueH.Services;

public static class PairingStatusMapper
{
    public const uint AudioVideoMajorClass = 4;

    public static PairingResult FromPairing(DevicePairingResultStatus status)
    {
        PairingOutcome outcome = status switch
        {
            DevicePairingResultStatus.Paired => PairingOutcome.Succeeded,
            DevicePairingResultStatus.AlreadyPaired => PairingOutcome.AlreadyDone,
            DevicePairingResultStatus.PairingCanceled or DevicePairingResultStatus.RejectedByHandler =>
                PairingOutcome.RejectedByUser,
            DevicePairingResultStatus.AuthenticationFailure
                or DevicePairingResultStatus.AuthenticationTimeout
                or DevicePairingResultStatus.AuthenticationNotAllowed => PairingOutcome.AuthenticationFailed,
            DevicePairingResultStatus.NotReadyToPair
                or DevicePairingResultStatus.ConnectionRejected
                or DevicePairingResultStatus.TooManyConnections => PairingOutcome.DeviceNotReady,
            _ => PairingOutcome.Failed
        };

        return new PairingResult(outcome, status.ToString());
    }

    public static PairingResult FromUnpairing(DeviceUnpairingResultStatus status)
    {
        PairingOutcome outcome = status switch
        {
            DeviceUnpairingResultStatus.Unpaired => PairingOutcome.Succeeded,
            DeviceUnpairingResultStatus.AlreadyUnpaired => PairingOutcome.AlreadyDone,
            _ => PairingOutcome.Failed
        };

        return new PairingResult(outcome, status.ToString());
    }

    public static PairingChallengeKind? ChallengeKindOf(DevicePairingKinds kind)
    {
        return kind switch
        {
            DevicePairingKinds.ConfirmOnly => PairingChallengeKind.ConfirmOnly,
            DevicePairingKinds.ConfirmPinMatch => PairingChallengeKind.ConfirmPinMatch,
            DevicePairingKinds.DisplayPin => PairingChallengeKind.DisplayPin,
            DevicePairingKinds.ProvidePin => PairingChallengeKind.ProvidePin,
            _ => null
        };
    }

    public static bool IsAudioCandidate(uint? majorClass, uint? minorClass)
    {
        if (majorClass is not (null or AudioVideoMajorClass))
        {
            return false;
        }

        return minorClass is null
            || BluetoothAudioDeviceKinds.FromMinorClass(minorClass) != BluetoothAudioDeviceKind.Unknown;
    }
}
