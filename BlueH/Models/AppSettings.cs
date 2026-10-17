namespace BlueH.Models;

public sealed record AppSettings(
    string? LanguageTag = null,
    bool IsDeveloperModeEnabled = false,
    IReadOnlyDictionary<Guid, NoiseControlMode>? NoiseControlModes = null)
{
    public static AppSettings Default { get; } = new();
}
