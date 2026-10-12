namespace BlueH.Models;

public sealed record AppSettings(string? LanguageTag = null, bool IsDeveloperModeEnabled = false)
{
    public static AppSettings Default { get; } = new();
}
