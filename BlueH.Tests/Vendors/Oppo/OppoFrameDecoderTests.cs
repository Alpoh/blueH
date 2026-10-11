using BlueH.Vendors.Oppo;

namespace BlueH.Tests.Vendors.Oppo;

[TestFixture]
public class OppoFrameDecoderTests
{
    private const string CapturedTouchAndBatteryResponses =
        "AA4100000881013A00000E010100000101010101010205010103000101040C0101050001010600020100000201010102010206020103000201040B0201050002010600AA0F000006810208000003016402640346";

    private const string CapturedFrameWithInconsistentLength =
        "AA3700000402033500F47B22636D64223A22666163746F72795F7265736574222C2265617273696465223A2252222C22636F756E74223A327D";

    [Test]
    public void TwoCapturedFramesInOneReadAreBothDecoded()
    {
        OppoFrameDecoder decoder = new();

        OppoDecodeResult result = decoder.Append(Convert.FromHexString(CapturedTouchAndBatteryResponses));

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames.Select(frame => frame.Command), Is.EqualTo(new ushort[] { 0x8108, 0x8106 }));
            Assert.That(result.Frames.Select(frame => frame.Sequence), Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(result.Frames[0].Payload, Has.Length.EqualTo(0x3A));
            Assert.That(Convert.ToHexString(result.Frames[1].Payload), Is.EqualTo("0003016402640346"));
            Assert.That(result.Problems, Is.Empty);
            Assert.That(decoder.BufferedByteCount, Is.Zero);
        });
    }

    [Test]
    public void AFrameSplitAcrossReadsIsDecodedOnceComplete()
    {
        byte[] bytes = Convert.FromHexString(CapturedTouchAndBatteryResponses);
        OppoFrameDecoder decoder = new();

        OppoDecodeResult first = decoder.Append(bytes.AsSpan(0, 30));
        OppoDecodeResult second = decoder.Append(bytes.AsSpan(30));

        Assert.Multiple(() =>
        {
            Assert.That(first.Frames, Is.Empty);
            Assert.That(first.Problems, Is.Empty);
            Assert.That(second.Frames, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void AFrameSplitInsideItsLengthPrefixWaits()
    {
        byte[] bytes = OppoFrameCodec.Encode(new OppoFrame(0x0204, Sequence: 1, new byte[200]));
        OppoFrameDecoder decoder = new();

        OppoDecodeResult first = decoder.Append(bytes.AsSpan(0, 2));
        OppoDecodeResult second = decoder.Append(bytes.AsSpan(2));

        Assert.Multiple(() =>
        {
            Assert.That(first.Frames, Is.Empty);
            Assert.That(first.Problems, Is.Empty);
            Assert.That(second.Frames.Single().Payload, Has.Length.EqualTo(200));
        });
    }

    [Test]
    public void BytesBeforeAPreambleAreSkippedAndReported()
    {
        OppoFrameDecoder decoder = new();
        byte[] garbage = [0x01, 0x02, 0x03];

        OppoDecodeResult result = decoder.Append([.. garbage, .. Convert.FromHexString(CapturedTouchAndBatteryResponses)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Has.Count.EqualTo(2));
            Assert.That(result.Problems.Single(), Does.Contain("3"));
        });
    }

    [Test]
    public void ACapturedFrameWithAnInconsistentLengthIsDiscardedAndReported()
    {
        OppoFrameDecoder decoder = new();

        OppoDecodeResult result = decoder.Append(Convert.FromHexString(CapturedFrameWithInconsistentLength));

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Is.Empty);
            Assert.That(result.Problems, Has.Count.EqualTo(1));
            Assert.That(decoder.BufferedByteCount, Is.Zero);
        });
    }

    [Test]
    public void AFrameAfterADiscardedOneIsStillDecoded()
    {
        OppoFrameDecoder decoder = new();
        byte[] valid = OppoFrameCodec.Encode(new OppoFrame(0x810C, Sequence: 3, [0x00, 0x01, 0x01, 0x08]));

        OppoDecodeResult result = decoder.Append([.. Convert.FromHexString(CapturedFrameWithInconsistentLength), .. valid]);

        Assert.That(result.Frames.Single().Sequence, Is.EqualTo(3));
    }

    [Test]
    public void AnImplausibleLengthDropsThePreambleAndResynchronises()
    {
        OppoFrameDecoder decoder = new();
        byte[] valid = OppoFrameCodec.Encode(new OppoFrame(0x810C, Sequence: 3, [0x00, 0x01, 0x01, 0x08]));

        OppoDecodeResult result = decoder.Append([0xAA, 0xFF, 0x7F, .. valid]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames.Single().Sequence, Is.EqualTo(3));
            Assert.That(result.Problems, Is.Not.Empty);
        });
    }

    [Test]
    public void ALengthShorterThanTheHeaderIsRejected()
    {
        OppoFrameDecoder decoder = new();

        OppoDecodeResult result = decoder.Append([0xAA, 0x03, 0x00, 0x00, 0x00]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Is.Empty);
            Assert.That(result.Problems, Is.Not.Empty);
        });
    }

    [Test]
    public void AnUnknownCommandIsStillAFrame()
    {
        OppoFrameDecoder decoder = new();

        OppoDecodeResult result = decoder.Append(OppoFrameCodec.Encode(new OppoFrame(0x7777, Sequence: 0, [0x05])));

        Assert.That(result.Frames.Single().Command, Is.EqualTo(0x7777));
    }

    [Test]
    public void ATruncatedFrameStaysBuffered()
    {
        OppoFrameDecoder decoder = new();
        byte[] valid = OppoFrameCodec.Encode(new OppoFrame(0x810C, Sequence: 3, [0x00, 0x01, 0x01, 0x08]));

        OppoDecodeResult result = decoder.Append(valid.AsSpan(0, valid.Length - 1));

        Assert.Multiple(() =>
        {
            Assert.That(result.Frames, Is.Empty);
            Assert.That(result.Problems, Is.Empty);
            Assert.That(decoder.BufferedByteCount, Is.EqualTo(valid.Length - 1));
        });
    }
}
