using BlueH.Models;
using BlueH.Services;
using Windows.Devices.Enumeration;

namespace BlueH.Tests.Services;

[TestFixture]
public class PairingStatusMapperTests
{
    [TestCase(DevicePairingResultStatus.Paired, PairingOutcome.Succeeded)]
    [TestCase(DevicePairingResultStatus.AlreadyPaired, PairingOutcome.AlreadyDone)]
    [TestCase(DevicePairingResultStatus.PairingCanceled, PairingOutcome.RejectedByUser)]
    [TestCase(DevicePairingResultStatus.RejectedByHandler, PairingOutcome.RejectedByUser)]
    [TestCase(DevicePairingResultStatus.AuthenticationFailure, PairingOutcome.AuthenticationFailed)]
    [TestCase(DevicePairingResultStatus.AuthenticationTimeout, PairingOutcome.AuthenticationFailed)]
    [TestCase(DevicePairingResultStatus.AuthenticationNotAllowed, PairingOutcome.AuthenticationFailed)]
    [TestCase(DevicePairingResultStatus.NotReadyToPair, PairingOutcome.DeviceNotReady)]
    [TestCase(DevicePairingResultStatus.ConnectionRejected, PairingOutcome.DeviceNotReady)]
    [TestCase(DevicePairingResultStatus.TooManyConnections, PairingOutcome.DeviceNotReady)]
    [TestCase(DevicePairingResultStatus.HardwareFailure, PairingOutcome.Failed)]
    [TestCase(DevicePairingResultStatus.NoSupportedProfiles, PairingOutcome.Failed)]
    [TestCase(DevicePairingResultStatus.Failed, PairingOutcome.Failed)]
    public void PairingStatusesMapToOutcomes(DevicePairingResultStatus status, PairingOutcome expected)
    {
        Assert.That(PairingStatusMapper.FromPairing(status).Outcome, Is.EqualTo(expected));
    }

    [Test]
    public void AnUnknownPairingStatusIsAFailureCarryingItsNumber()
    {
        const int UnknownStatus = 9999;

        PairingResult result = PairingStatusMapper.FromPairing((DevicePairingResultStatus)UnknownStatus);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(PairingOutcome.Failed));
            Assert.That(result.PlatformStatus, Is.EqualTo("9999"));
        });
    }

    [Test]
    public void ThePlatformStatusIsKeptForDiagnostics()
    {
        PairingResult result = PairingStatusMapper.FromPairing(DevicePairingResultStatus.AuthenticationTimeout);

        Assert.That(result.PlatformStatus, Is.EqualTo(nameof(DevicePairingResultStatus.AuthenticationTimeout)));
    }

    [TestCase(DeviceUnpairingResultStatus.Unpaired, PairingOutcome.Succeeded)]
    [TestCase(DeviceUnpairingResultStatus.AlreadyUnpaired, PairingOutcome.AlreadyDone)]
    [TestCase(DeviceUnpairingResultStatus.AccessDenied, PairingOutcome.Failed)]
    [TestCase(DeviceUnpairingResultStatus.OperationAlreadyInProgress, PairingOutcome.Failed)]
    [TestCase(DeviceUnpairingResultStatus.Failed, PairingOutcome.Failed)]
    public void UnpairingStatusesMapToOutcomes(DeviceUnpairingResultStatus status, PairingOutcome expected)
    {
        Assert.That(PairingStatusMapper.FromUnpairing(status).Outcome, Is.EqualTo(expected));
    }

    [TestCase(DevicePairingKinds.ConfirmOnly, PairingChallengeKind.ConfirmOnly)]
    [TestCase(DevicePairingKinds.ConfirmPinMatch, PairingChallengeKind.ConfirmPinMatch)]
    [TestCase(DevicePairingKinds.DisplayPin, PairingChallengeKind.DisplayPin)]
    [TestCase(DevicePairingKinds.ProvidePin, PairingChallengeKind.ProvidePin)]
    public void SupportedCeremoniesMapToChallenges(DevicePairingKinds kind, PairingChallengeKind expected)
    {
        Assert.That(PairingStatusMapper.ChallengeKindOf(kind), Is.EqualTo(expected));
    }

    [TestCase(DevicePairingKinds.None)]
    [TestCase(DevicePairingKinds.ProvidePasswordCredential)]
    [TestCase(DevicePairingKinds.ProvideAddress)]
    public void UnsupportedCeremoniesHaveNoChallenge(DevicePairingKinds kind)
    {
        Assert.That(PairingStatusMapper.ChallengeKindOf(kind), Is.Null);
    }

    [TestCase(4u, true)]
    [TestCase(null, true)]
    [TestCase(2u, false)]
    [TestCase(1u, false)]
    [TestCase(0u, false)]
    public void OnlyAudioOrUnknownMajorClassesAreOffered(uint? majorClass, bool expected)
    {
        Assert.That(PairingStatusMapper.IsAudioCandidate(majorClass), Is.EqualTo(expected));
    }
}
