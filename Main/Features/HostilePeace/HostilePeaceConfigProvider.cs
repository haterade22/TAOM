using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.HostilePeace;

public interface IHostilePeaceConfigProvider
{
    /// <summary>The JSON switches, read once. A missing or broken file gives the defaults (every block on).</summary>
    HostilePeaceConfig Config { get; }
}

/// <summary>Loads <c>ModuleData/diplomacy/hostile_peace.json</c> on first use. Four booleans need no range checks; a null
/// document or a parse error falls back to <see cref="HostilePeaceConfig"/>'s defaults and says so in the log.</summary>
public sealed class HostilePeaceConfigProvider : IHostilePeaceConfigProvider
{
    internal const string FileName = "hostile_peace.json";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private HostilePeaceConfig? _config;

    public HostilePeaceConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public HostilePeaceConfig Config => _config ??= Load();

    private HostilePeaceConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "diplomacy", FileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning($"[HostilePeace] {FileName} not found at {path}; every block stays on.");
            return new HostilePeaceConfig();
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HostilePeace] {FileName} could not be read, every block stays on: {ex.Message}");
            return new HostilePeaceConfig();
        }
    }

    /// <summary>The document, or the defaults for a <c>null</c> literal.</summary>
    internal static HostilePeaceConfig Parse(string json) =>
        JsonConvert.DeserializeObject<HostilePeaceConfig>(json) ?? new HostilePeaceConfig();
}
