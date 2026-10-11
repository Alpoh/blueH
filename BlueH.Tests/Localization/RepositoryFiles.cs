using System.IO;
using System.Runtime.CompilerServices;

namespace BlueH.Tests.Localization;

public static class RepositoryFiles
{
    private const string SolutionFileName = "BlueH.sln";

    public static string RootDirectory([CallerFilePath] string callerFilePath = "")
    {
        DirectoryInfo? directory = new FileInfo(callerFilePath).Directory;

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException($"{SolutionFileName} not found above {callerFilePath}");
    }

    public static string PropertiesDirectory()
    {
        return Path.Combine(RootDirectory(), "BlueH", "Properties");
    }

    public static string ApplicationDirectory()
    {
        return Path.Combine(RootDirectory(), "BlueH");
    }
}
