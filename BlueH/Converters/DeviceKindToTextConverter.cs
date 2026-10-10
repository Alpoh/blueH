using System.Globalization;
using System.Windows.Data;
using BlueH.Models;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.Converters;

public sealed class DeviceKindToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            BluetoothAudioDeviceKind.Headphones => AppResources.DeviceKindHeadphonesLabel,
            BluetoothAudioDeviceKind.Loudspeaker => AppResources.DeviceKindLoudspeakerLabel,
            _ => AppResources.DeviceKindUnknownLabel
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
