namespace BlueH.Models;

public enum PairingOutcome
{
    Succeeded,
    AlreadyDone,
    RejectedByUser,
    AuthenticationFailed,
    DeviceNotReady,
    Failed
}
