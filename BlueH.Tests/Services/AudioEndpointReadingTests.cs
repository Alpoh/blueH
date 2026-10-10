using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class AudioEndpointReadingTests
{
    [Test]
    public void TheMmDeviceIdIsExtractedFromARealEndpointId()
    {
        const string deviceInterfaceId =
            @"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{63a7089b-a6c3-4900-b04a-d95f0654486c}#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

        Assert.That(
            AudioEndpointIdReader.ExtractMmDeviceId(deviceInterfaceId),
            Is.EqualTo("{0.0.0.00000000}.{63a7089b-a6c3-4900-b04a-d95f0654486c}"));
    }

    [Test]
    public void AnIdThatIsAlreadyAnMmDeviceIdIsLeftAlone()
    {
        const string mmDeviceId = "{0.0.0.00000000}.{63a7089b-a6c3-4900-b04a-d95f0654486c}";

        Assert.That(AudioEndpointIdReader.ExtractMmDeviceId(mmDeviceId), Is.EqualTo(mmDeviceId));
    }

    [Test]
    public void AnIdInAnUnexpectedShapeIsLeftAlone()
    {
        Assert.That(AudioEndpointIdReader.ExtractMmDeviceId("something else"), Is.EqualTo("something else"));
    }

    [Test]
    public void AnEmptyIdIsHandled()
    {
        Assert.That(AudioEndpointIdReader.ExtractMmDeviceId(string.Empty), Is.Empty);
    }

    [Test]
    public void AnIdWithNothingAfterTheMarkerYieldsNothing()
    {
        Assert.That(AudioEndpointIdReader.ExtractMmDeviceId(@"\\?\SWD#MMDEVAPI#"), Is.Empty);
    }

    [TestCase(0f, 0)]
    [TestCase(1f, 100)]
    [TestCase(0.86f, 86)]
    [TestCase(0.76f, 76)]
    [TestCase(0.5f, 50)]
    [TestCase(0.005f, 1)]
    public void TheCoreAudioScalarBecomesAPercentage(float scalar, int expectedPercent)
    {
        Assert.That(VolumeScale.ToPercent(scalar), Is.EqualTo(expectedPercent));
    }

    [TestCase(-0.1f)]
    [TestCase(1.1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void AScalarOutsideTheRangeIsRejectedRatherThanClamped(float scalar)
    {
        Assert.That(VolumeScale.ToPercent(scalar), Is.Null);
    }

    [TestCase(0, 0f)]
    [TestCase(100, 1f)]
    [TestCase(50, 0.5f)]
    public void APercentageBecomesTheCoreAudioScalar(int percent, float expectedScalar)
    {
        Assert.That(VolumeScale.ToScalar(percent), Is.EqualTo(expectedScalar).Within(0.0001f));
    }

    [TestCase(-1)]
    [TestCase(101)]
    [TestCase(int.MaxValue)]
    public void APercentageOutsideTheRangeIsRefused(int percent)
    {
        Assert.That(VolumeScale.ToScalar(percent), Is.Null);
    }

    [Test]
    public void APercentageSurvivesARoundTripThroughTheScalar()
    {
        for (int percent = VolumeScale.MinimumPercent; percent <= VolumeScale.MaximumPercent; percent++)
        {
            float scalar = VolumeScale.ToScalar(percent)!.Value;

            Assert.That(VolumeScale.ToPercent(scalar), Is.EqualTo(percent));
        }
    }
}
