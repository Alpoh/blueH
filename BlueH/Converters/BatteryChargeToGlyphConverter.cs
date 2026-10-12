using System.Globalization;
using System.Windows.Data;
using BlueH.Models;
using BlueH.Themes;

namespace BlueH.Converters;

public sealed class BatteryChargeToGlyphConverter : IValueConverter
{
    private const int PercentPerStep = 100 / Glyphs.BatteryGlyphSteps;

    public static string GlyphFor(BatteryCharge charge)
    {
        if (charge.ChargePercent is not int percent)
        {
            return string.Empty;
        }

        int step = (int)Math.Round(percent / (double)PercentPerStep, MidpointRounding.AwayFromZero);

        return ((char)(Glyphs.EmptyBatteryCodePoint + step)).ToString();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is BatteryCharge charge ? GlyphFor(charge) : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
