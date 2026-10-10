namespace BlueH.Models;

public sealed record AppSettings(string? SelectedDeviceId)
{
    public static AppSettings Default { get; } = new(SelectedDeviceId: null);
}
