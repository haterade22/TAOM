using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TAOM.Tests.Features.Music;

internal static class MusicTestPaths
{
    public static readonly string RepositoryRootPath = FindRepositoryRoot();

    public static string ModuleRootPath => Path.Combine(RepositoryRootPath, "Main", "_Module");

    public static string ModuleDataPath => Path.Combine(ModuleRootPath, "ModuleData");

    public static string ModuleSoundsPath => Path.Combine(ModuleRootPath, "ModuleSounds");

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
    {
        var candidates = new[]
        {
            Environment.CurrentDirectory,
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(sourcePath ?? string.Empty)
        };

        foreach (var candidate in candidates)
        {
            var root = FindFrom(candidate);
            if (!string.IsNullOrEmpty(root))
                return root;
        }

        throw new DirectoryNotFoundException(
            "Could not locate TAOM repository root containing Main/_Module/ModuleData/project.mbproj.");
    }

    private static string FindFrom(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
            return null;

        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory != null)
        {
            var projectPath = Path.Combine(directory.FullName, "Main", "_Module", "ModuleData", "project.mbproj");
            if (File.Exists(projectPath))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }
}
