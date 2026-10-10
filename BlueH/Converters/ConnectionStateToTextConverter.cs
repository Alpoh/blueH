using System.Globalization;
using System.Windows.Data;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.Converters;

public sealed class ConnectionStateToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            true => AppResources.DeviceConnectedLabel,
            false => AppResources.DeviceDisconnectedLabel,
            _ => AppResources.DeviceConnectionUnknownLabel
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
