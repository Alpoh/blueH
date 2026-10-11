using System.Globalization;

namespace BlueH.Services;

public readonly record struct VolumePercentInput
{
    private VolumePercentInput(VolumeInputStatus status, int volumePercent)
    {
        Status = status;
        VolumePercent = volumePercent;
    }

    public VolumeInputStatus Status { get; }

    public int VolumePercent { get; }

    public bool IsValid => Status == VolumeInputStatus.Valid;

    public static VolumePercentInput Parse(string? text, CultureInfo culture)
    {
        if (!int.TryParse(text, NumberStyles.Integer, culture, out int volumePercent))
        {
            return new VolumePercentInput(VolumeInputStatus.NotANumber, VolumeScale.MinimumPercent);
        }

        return volumePercent is >= VolumeScale.MinimumPercent and <= VolumeScale.MaximumPercent
            ? new VolumePercentInput(VolumeInputStatus.Valid, volumePercent)
            : new VolumePercentInput(VolumeInputStatus.OutOfRange, volumePercent);
    }
}
