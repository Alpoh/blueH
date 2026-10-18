using BlueH.Vendors.Xiaomi;

namespace BlueH.Tests.Vendors.Xiaomi;

[TestFixture]
public class XiaomiFrameCodecTests
{
    private const string CapturedSetNoiseCancellingRequest = "FEDCBAC408000401020401EF";
    private const string CapturedReadNoiseControlRequest = "FEDCBAC4F3000301000BEF";

    [Test]
    public void ASetModeRequestIsEncodedExactlyAsTheEarbudsAcceptedIt()
    {
        byte[] encoded = XiaomiFrameCodec.Encode(new XiaomiFrame(0xC4, 0x08, [0x01, 0x02, 0x04, 0x01]));

        Assert.That(Convert.ToHexString(encoded), Is.EqualTo(CapturedSetNoiseCancellingRequest));
    }

    [Test]
    public void AReadRequestIsEncodedExactlyAsTheEarbudsAnsweredIt()
    {
        byte[] encoded = XiaomiFrameCodec.Encode(new XiaomiFrame(0xC4, 0xF3, [0x01, 0x00, 0x0B]));

        Assert.That(Convert.ToHexString(encoded), Is.EqualTo(CapturedReadNoiseControlRequest));
    }

    [Test]
    public void TheLengthIsBigEndianAndCountsOnlyTheBody()
    {
        byte[] encoded = XiaomiFrameCodec.Encode(new XiaomiFrame(0xC4, 0x08, new byte[0x0102]));

        Assert.Multiple(() =>
        {
            Assert.That(encoded[5], Is.EqualTo(0x01));
            Assert.That(encoded[6], Is.EqualTo(0x02));
            Assert.That(encoded, Has.Length.EqualTo(XiaomiFrameCodec.HeaderLength + 0x0102 + 1));
            Assert.That(encoded[^1], Is.EqualTo(XiaomiFrameCodec.Trailer));
        });
    }

    [Test]
    public void AFrameSurvivesAnEncodeDecodeRoundTrip()
    {
        XiaomiFrame original = new(0xC7, 0xF4, [0x09, 0x04, 0x00, 0x0B, 0x01, 0x00]);
        XiaomiFrameDecoder decoder = new();

        XiaomiDecodeResult result = decoder.Append(XiaomiFrameCodec.Encode(original));

        Assert.That(result.Frames.Single(), Is.EqualTo(original).Using<XiaomiFrame>(SameFrame));
    }

    private static bool SameFrame(XiaomiFrame left, XiaomiFrame right)
    {
        return left.Type == right.Type && left.Opcode == right.Opcode && left.Body.SequenceEqual(right.Body);
    }
}
