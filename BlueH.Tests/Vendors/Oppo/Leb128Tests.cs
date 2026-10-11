using BlueH.Vendors.Oppo;

namespace BlueH.Tests.Vendors.Oppo;

[TestFixture]
public class Leb128Tests
{
    [TestCase(0u, new byte[] { 0x00 })]
    [TestCase(9u, new byte[] { 0x09 })]
    [TestCase(127u, new byte[] { 0x7F })]
    [TestCase(128u, new byte[] { 0x80, 0x01 })]
    [TestCase(155u, new byte[] { 0x9B, 0x01 })]
    [TestCase(2048u, new byte[] { 0x80, 0x10 })]
    public void ValuesEncodeToTheKnownBytes(uint value, byte[] expected)
    {
        Assert.That(Leb128.Encode(value), Is.EqualTo(expected));
    }

    [TestCase(0u)]
    [TestCase(127u)]
    [TestCase(128u)]
    [TestCase(16_383u)]
    [TestCase(16_384u)]
    [TestCase(uint.MaxValue)]
    public void DecodingReversesEncoding(uint value)
    {
        byte[] encoded = Leb128.Encode(value);

        Leb128Read read = Leb128.TryDecode(encoded, 0);

        Assert.Multiple(() =>
        {
            Assert.That(read.Status, Is.EqualTo(Leb128ReadStatus.Complete));
            Assert.That(read.Value, Is.EqualTo(value));
            Assert.That(read.EncodedLength, Is.EqualTo(encoded.Length));
        });
    }

    [Test]
    public void DecodingStartsAtTheGivenOffset()
    {
        Leb128Read read = Leb128.TryDecode([0xAA, 0x9B, 0x01], 1);

        Assert.That(read.Value, Is.EqualTo(155u));
    }

    [Test]
    public void AContinuationWithoutItsNextByteIsIncomplete()
    {
        Assert.That(Leb128.TryDecode([0x9B], 0).Status, Is.EqualTo(Leb128ReadStatus.Incomplete));
    }

    [Test]
    public void MoreThanFiveContinuationBytesIsMalformed()
    {
        Assert.That(
            Leb128.TryDecode([0x80, 0x80, 0x80, 0x80, 0x80, 0x01], 0).Status,
            Is.EqualTo(Leb128ReadStatus.Malformed));
    }
}
