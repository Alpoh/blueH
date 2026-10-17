using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlueH.Models;

namespace BlueH.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private const string NullContentFailureDetail = "The settings file deserialized to null.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BlueH",
        "settings.json");

    public SettingsLoad Load()
    {
        if (!File.Exists(_filePath))
        {
            return new SettingsLoad(AppSettings.Default, SettingsSource.DefaultsBecauseMissing, FailureDetail: null);
        }

        try
        {
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), SerializerOptions);

            return settings is null
                ? new SettingsLoad(AppSettings.Default, SettingsSource.DefaultsBecauseUnreadable, NullContentFailureDetail)
                : new SettingsLoad(settings, SettingsSource.File, FailureDetail: null);
        }
        catch (Exception failure) when (failure is JsonException or IOException or UnauthorizedAccessException)
        {
            return new SettingsLoad(AppSettings.Default, SettingsSource.DefaultsBecauseUnreadable, failure.Message);
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
