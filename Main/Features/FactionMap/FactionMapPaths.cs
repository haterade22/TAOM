using TAOM.Core.Logging;

namespace TAOM.Features.FactionMap;

public static class FactionMapPaths
{
    public static string ModulePath { get; set; } = "";

    private static IModLogger? _logger;

    public static void Initialize(string modulePath, IModLogger logger)
    {
        ModulePath = modulePath;
        _logger = logger;
    }

    public static void Log(string message) => _logger?.LogInfo(message);
    public static void LogError(string message) => _logger?.LogError(message);
}
