using System.Globalization;
using System.Windows.Data;
using BlueH.Models;
using BlueH.Themes;

namespace BlueH.Converters;

public sealed class DeviceKindToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            BluetoothAudioDeviceKind.Headphones => Glyphs.Headphones,
            BluetoothAudioDeviceKind.Loudspeaker => Glyphs.Loudspeaker,
            _ => Glyphs.Bluetooth
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
