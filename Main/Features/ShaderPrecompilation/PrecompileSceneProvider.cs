using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderPrecompilation;

// Battle scenes to walk for terrain/atmosphere shader coverage. Defaults to the TAOM worldmap
// custom battle scenes (the `taom_*_battle_*_forceatmo` set — the class that AV'd d3dcompiler in
// #287). Override/extend via a plain-text config (one scene id per line, `#` comments) at
// ModuleData/shader_precompilation/precompile_scenes.txt — no JSON dependency, trivially editable.
public sealed class PrecompileSceneProvider : IPrecompileSceneProvider
{
    private const string ConfigRelPath = "shader_precompilation/precompile_scenes.txt";

    // The worldmap-grid scenes battles actually use (TAOM_Map/SceneObj). These are the ones that
    // runtime-compile their terrain + forced-atmosphere shaders on entry today.
    public static readonly IReadOnlyList<string> DefaultScenes = new[]
    {
        "taom_mordor_battle_001_forceatmo",
        "taom_mordor_battle_002_forceatmo",
        "taom_mordor_battle_003_forceatmo",
        "taom_mordor_battle_004_forceatmo",
        "taom_mordor_battle_black_gates_forceatmo",
        "taom_mordor_battle_dead_marshes_forceatmo",
        "taom_rohan_battle_001_forceatmo",
        "taom_rohan_battle_fords_of_isen_forceatmo",
        // NOTE: taom_dwarves_battle_001_forceatmo exists on disk but is NOT in custom_battle_scenes.xml
        // (nor any worldmap/battle data) — it can't load as a custom battle and is never used in a real
        // battle, so precompiling it has no value and would just time out. Excluded (Codex 2026-06-17).
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public PrecompileSceneProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<string> GetScenes()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, ConfigRelPath);
            if (File.Exists(path))
            {
                var parsed = ParseSceneList(File.ReadAllText(path));
                if (parsed.Count > 0)
                {
                    _logger?.LogInfo($"[ShaderPrecompilation] {parsed.Count} scenes from {ConfigRelPath}");
                    return parsed;
                }
                _logger?.LogWarning($"[ShaderPrecompilation] {ConfigRelPath} had no usable scene ids — using {DefaultScenes.Count} defaults");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] scene config read failed ({ex.GetType().Name}) — using defaults");
        }
        return DefaultScenes;
    }

    // Pure: one scene id per line; trims; drops blanks and `#` comments; de-dupes (ordinal-ignore-case,
    // first-wins order preserved).
    public static IReadOnlyList<string> ParseSceneList(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (seen.Add(line)) result.Add(line);
        }
        return result;
    }
}
