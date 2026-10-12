using System.Globalization;
using BlueH.Converters;
using BlueH.Models;
using BlueH.Themes;

namespace BlueH.Tests.Converters;

[TestFixture]
public class DeviceGlyphConverterTests
{
    [TestCase(BluetoothAudioDeviceKind.Headphones, Glyphs.Headphones)]
    [TestCase(BluetoothAudioDeviceKind.Loudspeaker, Glyphs.Loudspeaker)]
    [TestCase(BluetoothAudioDeviceKind.Unknown, Glyphs.Bluetooth)]
    public void EachKindHasItsGlyph(BluetoothAudioDeviceKind kind, string expected)
    {
        object glyph = new DeviceKindToGlyphConverter().Convert(kind, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.That(glyph, Is.EqualTo(expected));
    }

    [TestCase(0, '')]
    [TestCase(4, '')]
    [TestCase(5, '')]
    [TestCase(50, '')]
    [TestCase(80, '')]
    [TestCase(94, '')]
    [TestCase(95, '')]
    [TestCase(100, '')]
    public void TheBatteryGlyphFollowsTheChargeInTenSteps(int percent, char expected)
    {
        string glyph = BatteryChargeToGlyphConverter.GlyphFor(BatteryCharge.FromPercent(percent));

        Assert.That(glyph, Is.EqualTo(expected.ToString()));
    }

    [Test]
    public void AnUnknownBatteryHasNoGlyph()
    {
        Assert.That(BatteryChargeToGlyphConverter.GlyphFor(BatteryCharge.Unknown), Is.Empty);
    }

    [Test]
    public void EveryGlyphIsOnePrivateUseCharacter()
    {
        string[] glyphs = [.. typeof(Glyphs)
            .GetFields()
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetValue(null)!)];

        Assert.That(glyphs, Is.All.Matches<string>(glyph => glyph.Length == 1 && glyph[0] is >= '' and <= ''));
    }
}
