using BlueH.Vendors.Xiaomi;

namespace BlueH.Tests.Vendors.Xiaomi;

[TestFixture]
public class XiaomiFrameDecoderTests
{
    private const string CapturedReadResponse = "FEDCBA04F30007000104000B0000EF";
    private const string CapturedNotificationThenAck = "FEDCBAC7F400060904000B0100EF" + "FEDCBA040800020001EF";

    [Test]
    public void ACapturedResponseIsDecoded()
    {
        XiaomiFrameDecoder decoder = new();

        XiaomiDecodeResult result = decoder.Append(Convert.FromHexString(CapturedReadResponse));

        XiaomiFrame frame = result.Frames.Single();
        Assert.Multiple(() =>
        {
            Assert.That(frame.Type, Is.EqualTo(0x04));
            Assert.That(frame.Opcode, Is.EqualTo(0xF3));
            Assert.That(Convert.ToHexString(frame.Body), Is.EqualTo("000104000B0000"));
            Assert.That(result.Problems, Is.Empty);
            Assert.That(decoder.BufferedByteCount, Is.Zero);
        });
    }

    [Test]
    public void TwoCapturedFramesInOneReadAreBothDecoded()
    {
        XiaomiFrameDecoder decoder = new();

        XiaomiDecodeResult result = decoder.Append(Convert.FromHexString(CapturedNotificationThenAck));

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames.Select(frame => frame.Opcode), Is.EqualTo(new byte[] { 0xF4, 0x08 }));
            Assert.That(result.Problems, Is.Empty);
        });
    }

    [Test]
    public void AFrameSplitAtAnyByteIsDecodedOnceComplete()
    {
        byte[] bytes = Convert.FromHexString(CapturedReadResponse);

        for (int split = 1; split < bytes.Length; split++)
        {
            XiaomiFrameDecoder decoder = new();

            XiaomiDecodeResult first = decoder.Append(bytes.AsSpan(0, split));
            XiaomiDecodeResult second = decoder.Append(bytes.AsSpan(split));

            Assert.Multiple(() =>
            {
                Assert.That(first.Frames, Is.Empty, $"split at {split}");
                Assert.That(first.Problems, Is.Empty, $"split at {split}");
                Assert.That(second.Frames, Has.Count.EqualTo(1), $"split at {split}");
                Assert.That(second.Problems, Is.Empty, $"split at {split}");
            });
        }
    }

    [Test]
    public void BytesBeforeAPreambleAreSkippedAndReported()
    {
        XiaomiFrameDecoder decoder = new();
        byte[] garbage = [0x01, 0xFE, 0x03];

        XiaomiDecodeResult result = decoder.Append([.. garbage, .. Convert.FromHexString(CapturedReadResponse)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Has.Count.EqualTo(1));
            Assert.That(result.Problems.Single(), Does.Contain("3"));
        });
    }

    [Test]
    public void AFrameWithoutItsTrailerIsDiscardedAndTheNextOneStillDecodes()
    {
        byte[] broken = Convert.FromHexString(CapturedReadResponse);
        broken[^1] = 0x00;
        XiaomiFrameDecoder decoder = new();

        XiaomiDecodeResult result = decoder.Append([.. broken, .. Convert.FromHexString(CapturedNotificationThenAck)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames.Select(frame => frame.Opcode), Is.EqualTo(new byte[] { 0xF4, 0x08 }));
            Assert.That(result.Problems, Is.Not.Empty);
        });
    }

    [Test]
    public void AnImplausibleLengthIsReportedInsteadOfWaitedFor()
    {
        XiaomiFrameDecoder decoder = new();

        XiaomiDecodeResult result = decoder.Append(Convert.FromHexString("FEDCBA04F3FFFF00"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Is.Empty);
            Assert.That(result.Problems, Has.Some.Contains("65535"));
            Assert.That(decoder.BufferedByteCount, Is.Zero);
        });
    }

    [Test]
    public void TrailingBytesThatAreNotAPreambleAreNotKeptForever()
    {
        XiaomiFrameDecoder decoder = new();

        decoder.Append([0x01, 0x02, 0x03, 0x04]);

        Assert.That(decoder.BufferedByteCount, Is.Zero);
    }
}
