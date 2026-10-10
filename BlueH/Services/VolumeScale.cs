namespace BlueH.Services;

public static class VolumeScale
{
    public const int MinimumPercent = 0;
    public const int MaximumPercent = 100;

    private const float MinimumScalar = 0f;
    private const float MaximumScalar = 1f;

    public static int? ToPercent(float scalar)
    {
        if (float.IsNaN(scalar) || scalar < MinimumScalar || scalar > MaximumScalar)
        {
            return null;
        }

        return (int)Math.Round(scalar * MaximumPercent, MidpointRounding.AwayFromZero);
    }

    public static float? ToScalar(int percent)
    {
        if (percent < MinimumPercent || percent > MaximumPercent)
        {
            return null;
        }

        return (float)percent / MaximumPercent;
    }
}
