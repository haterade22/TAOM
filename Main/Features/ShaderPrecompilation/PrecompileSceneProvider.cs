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

    // The TAOM-authored scenes (TAOM_Map/SceneObj) that runtime-compile their terrain + forced-atmosphere
    // shaders on entry — every one ships header-only (no compressed_shader_cache.sack) = the #287 class.
    // Open-field battles + custom siege settlement scenes + custom village scenes (all load by id via the
    // same "Battle" ScenePass; the walk bypasses custom_battle_scenes.xml). Mirrors precompile_scenes.txt.
    //
    // SOURCE OF TRUTH: keep this list in sync with `precompile_scenes.txt` (the live override). The
    // commented-out crashers below mirror the disabled set there — they ship `_forceatmo` but their
    // pbr_terrain vista permutation hard-crashes some GPUs on scene load, so a missing/empty config must
    // NOT resurrect them via this fallback (the 2026-06-25 fallback-drift fix; pinned by
    // PrecompileSceneProviderParseTests.DefaultScenes_ExcludesDisabledCrashScenes). Re-enable together once
    // the native shader-compile-guard hook lands (#287).
    public static readonly IReadOnlyList<string> DefaultScenes = new[]
    {
        // Open-field battle scenes — ALL DISABLED (pbr_terrain vista-permutation GPU crash on load).
        // Mordor DISABLED 2026-06-25 (was the fallback-drift: uncommented here while disabled in the live
        // config). Also removed from sp_battle_scenes.xml so real battles fall back to vanilla terrain.
        // "taom_mordor_battle_001_forceatmo",
        // "taom_mordor_battle_002_forceatmo",
        // "taom_mordor_battle_003_forceatmo",
        // "taom_mordor_battle_004_forceatmo",
        // "taom_mordor_battle_black_gates_forceatmo",
        // "taom_mordor_battle_dead_marshes_forceatmo",
        // Rohan field-battle scenes DISABLED 2026-06-19 (pbr_terrain input-layout-9 GPU crash; also removed
        // from sp_battle_scenes.xml so real battles fall back to vanilla terrain). Re-enable with the shader override.
        // "taom_rohan_battle_001_forceatmo",
        // "taom_rohan_battle_fords_of_isen_forceatmo",
        // Custom siege settlement scenes (loaded via the Battle path; siege-engine-material coverage probed in-game)
        "taom_gondor_castle_001_forceatmo",
        "taom_gondor_castle_002_forceatmo",
        "taom_gondor_castle_003_forceatmo",
        "taom_gondor_town_minas_tirith_forceatmo",
        "taom_gondor_town_osgiliath_w_forceatmo",
        "taom_gondor_town_osgiliath_e_forceatmo",
        "taom_gondor_town_lossarnach_forceatmo",
        "taom_isengard_town_orthanc_forceatmo",
        // "taom_rohan_castle_helms_deep_forceatmo",  // DISABLED 2026-06-19: same Rohan pbr_terrain input-layout-9 crash class
        // Custom village scenes (66 settlement instances)
        "taom_gondor_village_001_forceatmo",
        "taom_gondor_village_002_forceatmo",
        "taom_gondor_village_003_forceatmo",
        "taom_gondor_village_004_forceatmo",
        // EXCLUDED: taom_dwarves_battle_001_forceatmo + taom_mordor_town_goblin_town_forceatmo (orphans,
        // 0 settlements); lotrtaom_iron_hills_01_forceatmo (scene.xscene CTDs on load — separate crash class).
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
