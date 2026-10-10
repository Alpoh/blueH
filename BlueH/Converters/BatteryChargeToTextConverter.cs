using System.Globalization;
using System.Windows.Data;
using BlueH.Models;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.Converters;

public sealed class BatteryChargeToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not BatteryCharge charge || charge.ChargePercent is not int chargePercent)
        {
            return AppResources.DeviceBatteryUnknownLabel;
        }

        return string.Format(culture, AppResources.DeviceBatteryPercentFormat, chargePercent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
