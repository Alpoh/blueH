using System.Globalization;
using BlueH.Services;

namespace BlueH.Tests.Services;

[TestFixture]
public class VolumePercentInputTests
{
    [TestCase("0", 0)]
    [TestCase("50", 50)]
    [TestCase("100", 100)]
    [TestCase("  42  ", 42)]
    public void AWholeNumberWithinTheRangeIsAccepted(string text, int expectedVolumePercent)
    {
        VolumePercentInput input = VolumePercentInput.Parse(text, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(input.Status, Is.EqualTo(VolumeInputStatus.Valid));
            Assert.That(input.IsValid, Is.True);
            Assert.That(input.VolumePercent, Is.EqualTo(expectedVolumePercent));
        });
    }

    [TestCase("101")]
    [TestCase("-1")]
    [TestCase("1000")]
    public void AWholeNumberOutsideTheRangeIsRejectedRatherThanClamped(string text)
    {
        VolumePercentInput input = VolumePercentInput.Parse(text, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(input.Status, Is.EqualTo(VolumeInputStatus.OutOfRange));
            Assert.That(input.IsValid, Is.False);
        });
    }

    [TestCase("abc")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("50.5")]
    [TestCase("1 000")]
    [TestCase("8%")]
    public void TextThatIsNotAWholeNumberIsRejected(string? text)
    {
        VolumePercentInput input = VolumePercentInput.Parse(text, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(input.Status, Is.EqualTo(VolumeInputStatus.NotANumber));
            Assert.That(input.IsValid, Is.False);
        });
    }

    [Test]
    public void TheCultureDecidesHowTheNumberIsRead()
    {
        CultureInfo spanish = CultureInfo.GetCultureInfo("es-ES");

        Assert.Multiple(() =>
        {
            Assert.That(VolumePercentInput.Parse("50", spanish).VolumePercent, Is.EqualTo(50));
            Assert.That(
                VolumePercentInput.Parse("50,5", spanish).Status,
                Is.EqualTo(VolumeInputStatus.NotANumber));
        });
    }
}
