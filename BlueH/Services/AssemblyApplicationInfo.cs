using System.Reflection;

namespace BlueH.Services;

public sealed class AssemblyApplicationInfo : IApplicationInfo
{
    private const char BuildMetadataSeparator = '+';

    public AssemblyApplicationInfo(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Version = WithoutBuildMetadata(informational) ?? assembly.GetName().Version?.ToString() ?? string.Empty;
    }

    public string Version { get; }

    public static string? WithoutBuildMetadata(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return null;
        }

        int separator = informationalVersion.IndexOf(BuildMetadataSeparator, StringComparison.Ordinal);

        return separator < 0 ? informationalVersion : informationalVersion[..separator];
    }
}
