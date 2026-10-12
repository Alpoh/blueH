using System.Windows.Media;
using BlueH.Converters;

namespace BlueH.Tests.Converters;

[TestFixture]
public class ImageBytesToImageSourceConverterTests
{
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Test]
    public void APngIsDecodedIntoAFrozenImage()
    {
        ImageSource? image = ImageBytesToImageSourceConverter.Decode(Convert.FromBase64String(OnePixelPng));

        Assert.Multiple(() =>
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image!.Width, Is.EqualTo(1));
            Assert.That(image.IsFrozen, Is.True);
        });
    }

    [Test]
    public void BytesThatAreNotAnImageGiveNoImage()
    {
        Assert.That(ImageBytesToImageSourceConverter.Decode([0x01, 0x02, 0x03, 0x04]), Is.Null);
    }

    [Test]
    public void NoBytesGiveNoImage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ImageBytesToImageSourceConverter.Decode(null), Is.Null);
            Assert.That(ImageBytesToImageSourceConverter.Decode([]), Is.Null);
        });
    }
}
