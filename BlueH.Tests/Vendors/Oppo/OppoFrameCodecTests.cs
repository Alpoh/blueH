using BlueH.Vendors.Oppo;

namespace BlueH.Tests.Vendors.Oppo;

[TestFixture]
public class OppoFrameCodecTests
{
    [Test]
    public void ANoiseControlRequestEncodesToTheDocumentedBytes()
    {
        byte[] encoded = OppoFrameCodec.Encode(new OppoFrame(0x010C, Sequence: 0, [0x01, 0x01]));

        Assert.That(Convert.ToHexString(encoded), Is.EqualTo("AA0900000C01000200" + "0101"));
    }

    [Test]
    public void TheCommandAndLengthsAreLittleEndian()
    {
        byte[] encoded = OppoFrameCodec.Encode(new OppoFrame(0x0404, Sequence: 7, [0x01, 0x01, 0x08]));

        Assert.That(Convert.ToHexString(encoded), Is.EqualTo("AA0A00000404070300" + "010108"));
    }

    [Test]
    public void ALongPayloadUsesAMultiByteLength()
    {
        byte[] payload = new byte[200];

        byte[] encoded = OppoFrameCodec.Encode(new OppoFrame(0x0204, Sequence: 1, payload));

        Assert.Multiple(() =>
        {
            Assert.That(encoded[1..3], Is.EqualTo(new byte[] { 0xCF, 0x01 }));
            Assert.That(encoded, Has.Length.EqualTo(1 + 2 + OppoFrameCodec.HeaderLength + payload.Length));
        });
    }

    [Test]
    public void EncodingThenDecodingRoundTrips()
    {
        OppoFrame original = new(0x810C, Sequence: 42, [0x00, 0x01, 0x01, 0x02]);
        byte[] encoded = OppoFrameCodec.Encode(original);

        OppoFrame? decoded = OppoFrameCodec.DecodePacket(encoded.AsSpan(2));

        Assert.Multiple(() =>
        {
            Assert.That(decoded?.Command, Is.EqualTo(original.Command));
            Assert.That(decoded?.Sequence, Is.EqualTo(original.Sequence));
            Assert.That(decoded?.Payload, Is.EqualTo(original.Payload));
        });
    }

    [Test]
    public void APacketShorterThanItsHeaderIsRejected()
    {
        Assert.That(OppoFrameCodec.DecodePacket([0x00, 0x00, 0x0C, 0x81, 0x01, 0x00]), Is.Null);
    }

    [TestCase("00000C81010500000101", TestName = "A declared payload longer than the packet is rejected")]
    [TestCase("00000C81010100000101", TestName = "A declared payload shorter than the packet is rejected")]
    public void APacketWhosePayloadLengthDisagreesIsRejected(string packet)
    {
        Assert.That(OppoFrameCodec.DecodePacket(Convert.FromHexString(packet)), Is.Null);
    }

    [Test]
    public void AResponseIsRecognisedByItsFlag()
    {
        OppoFrame response = new(0x810C, Sequence: 0, []);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsResponseTo(0x010C), Is.True);
            Assert.That(response.IsResponseTo(0x0404), Is.False);
        });
    }
}
