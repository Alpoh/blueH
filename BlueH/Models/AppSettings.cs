namespace BlueH.Models;

public sealed record AppSettings(string? LanguageTag = null)
{
    public static AppSettings Default { get; } = new();
}
