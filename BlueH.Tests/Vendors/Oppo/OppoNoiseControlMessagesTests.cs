using BlueH.Models;
using BlueH.Vendors.Oppo;

namespace BlueH.Tests.Vendors.Oppo;

[TestFixture]
public class OppoNoiseControlMessagesTests
{
    [Test]
    public void TheModeRequestAsksForTheModeSetting()
    {
        OppoFrame request = OppoNoiseControlMessages.ModeRequest(sequence: 5);

        Assert.Multiple(() =>
        {
            Assert.That(request.Command, Is.EqualTo(0x010C));
            Assert.That(request.Sequence, Is.EqualTo(5));
            Assert.That(request.Payload, Is.EqualTo(new byte[] { 0x01, 0x01 }));
        });
    }

    [TestCase(NoiseControlMode.Off, (byte)0x01)]
    [TestCase(NoiseControlMode.Transparency, (byte)0x02)]
    [TestCase(NoiseControlMode.NoiseCancelling, (byte)0x08)]
    public void SettingAModeSendsItsCode(NoiseControlMode mode, byte code)
    {
        OppoFrame set = OppoNoiseControlMessages.SetMode(mode, sequence: 0);

        Assert.Multiple(() =>
        {
            Assert.That(set.Command, Is.EqualTo(0x0404));
            Assert.That(set.Payload, Is.EqualTo(new byte[] { 0x01, 0x01, code }));
        });
    }

    [TestCase(new byte[] { 0x00, 0x01, 0x01, 0x08 }, NoiseControlMode.NoiseCancelling)]
    [TestCase(new byte[] { 0x00, 0x01, 0x02 }, NoiseControlMode.Transparency)]
    [TestCase(new byte[] { 0x00, 0x01, 0x01, 0x01 }, NoiseControlMode.Off)]
    public void BothResponseShapesAreDecoded(byte[] payload, NoiseControlMode expected)
    {
        Assert.That(OppoNoiseControlMessages.DecodeModeResponse(payload), Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x00, 0x01, 0x01, 0x04 })]
    [TestCase(new byte[] { 0x01, 0x01, 0x01, 0x08 })]
    [TestCase(new byte[] { 0x00, 0x02, 0x01, 0x08 })]
    [TestCase(new byte[] { 0x00, 0x01 })]
    [TestCase(new byte[] { 0x00, 0x01, 0x01, 0x08, 0x00 })]
    [TestCase(new byte[0])]
    public void AnythingElseDecodesToNoMode(byte[] payload)
    {
        Assert.That(OppoNoiseControlMessages.DecodeModeResponse(payload), Is.Null);
    }

    [Test]
    public void AnAcknowledgementSucceedsOnlyWithAZeroStatus()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OppoNoiseControlMessages.IsSuccessfulAcknowledgement([0x00]), Is.True);
            Assert.That(OppoNoiseControlMessages.IsSuccessfulAcknowledgement([0x01]), Is.False);
            Assert.That(OppoNoiseControlMessages.IsSuccessfulAcknowledgement([]), Is.False);
        });
    }
}
