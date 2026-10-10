using System.Globalization;

namespace BlueH.Time;

public static class CanonicalTimestamp
{
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    public const string DateFormat = "yyyy-MM-dd";

    private const DateTimeStyles ParseStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public static string Format(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatDateOnly(DateTimeOffset instant)
    {
        return instant.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? text, out DateTimeOffset instant)
    {
        return DateTimeOffset.TryParseExact(
            text,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            ParseStyles,
            out instant);
    }
}
