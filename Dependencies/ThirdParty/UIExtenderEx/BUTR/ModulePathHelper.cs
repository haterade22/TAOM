using System;
using System.IO;

namespace Bannerlord.BUTR.Shared.Helpers;

/// <summary>
/// Minimal replacement for ModuleInfoHelper.GetModulePath().
/// Walks up from the assembly location to find the module root (directory containing SubModule.xml).
/// </summary>
internal static class ModulePathHelper
{
    public static string GetModulePath(Type type)
    {
        var location = type.Assembly.Location;
        if (string.IsNullOrWhiteSpace(location))
            return string.Empty;

        var dir = new DirectoryInfo(Path.GetFullPath(location)).Parent;
        while (dir?.Parent != null && dir.Exists)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SubModule.xml")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return string.Empty;
    }
}
