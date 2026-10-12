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

    [TestCase(4u, null, true)]
    [TestCase(null, null, true)]
    [TestCase(2u, null, false)]
    [TestCase(1u, null, false)]
    [TestCase(0u, null, false)]
    public void OnlyAudioOrUnknownMajorClassesAreOffered(uint? majorClass, uint? minorClass, bool expected)
    {
        Assert.That(PairingStatusMapper.IsAudioCandidate(majorClass, minorClass), Is.EqualTo(expected));
    }

    [TestCase(1u, true, TestName = "A wearable headset is offered")]
    [TestCase(2u, true, TestName = "A hands-free kit is offered")]
    [TestCase(6u, true, TestName = "Headphones are offered")]
    [TestCase(5u, true, TestName = "A loudspeaker is offered")]
    [TestCase(15u, false, TestName = "A display with loudspeaker such as a TV is not offered")]
    [TestCase(12u, false, TestName = "A video camera is not offered")]
    [TestCase(8u, false, TestName = "A car audio unit is not offered")]
    public void WithinAudioOnlyHeadphonesAndSpeakersAreOffered(uint minorClass, bool expected)
    {
        Assert.That(PairingStatusMapper.IsAudioCandidate(4u, minorClass), Is.EqualTo(expected));
    }
}
