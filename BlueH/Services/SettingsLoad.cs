using BlueH.Models;

namespace BlueH.Services;

public sealed record SettingsLoad(AppSettings Settings, SettingsSource Source, string? FailureDetail);
