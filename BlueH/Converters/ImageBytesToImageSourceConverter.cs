using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlueH.Converters;

public sealed class ImageBytesToImageSourceConverter : IValueConverter
{
    public static ImageSource? Decode(byte[]? imageBytes)
    {
        if (imageBytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new(imageBytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception failure) when (failure is NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Decode(value as byte[]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
