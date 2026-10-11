namespace BlueH.Models;

public sealed record AppSettings(string? SelectedDeviceId, string? LanguageTag = null)
{
    public static AppSettings Default { get; } = new(SelectedDeviceId: null);
}
