using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.ShaderPrecompilation.Domain;

namespace TAOM.Features.ShaderPrecompilation;

// Pure builder for the ordered precompile work list: the character batches first (every loaded
// character exactly once, sliced into batches of at most DefaultCharacterBatchSize), then one
// ScenePass per battle scene (terrain + forced-atmosphere shaders, the #287 class).
//
// Why batches rather than one big battle (#560): the engine's CustomBattle preload view hands EVERY
// roster character to PreloadHelper, which compiles every equipment mesh's shaders, so coverage is
// decided by roster membership, not by how many agents spawn. One battle holding the whole roster
// (roughly 5,000 characters on 1.4.8) would keep every Armory item resident at once, the memory
// shape TAOM has crashed on before (#385); a batch bounds that per item.
public static class ShaderPrecompilePlanner
{
    // The character batches run on the always-present default custom-battle scene.
    public const string CharacterBattleScene = "battle_terrain_029";

    // Characters per batch. A 4,600 to 5,300 roster becomes 5 or 6 batches, roughly five minutes of
    // load and teardown overhead in total, while each batch preloads about a fifth of what one
    // battle already carried on the dev machine. Re-tune from the walk's per-batch memory lines.
    public const int DefaultCharacterBatchSize = 1000;

    public static int CountBatches(int rosterCount, int batchSize = DefaultCharacterBatchSize)
    {
        RequireBatchSize(batchSize);
        return rosterCount <= 0 ? 0 : (rosterCount + batchSize - 1) / batchSize;
    }

    // The full plan once the roster is known: batches, then scenes.
    public static IReadOnlyList<PrecompileItem> BuildPlan(IEnumerable<string> characterIds, IEnumerable<string> sceneIds,
        int batchSize = DefaultCharacterBatchSize)
    {
        RequireBatchSize(batchSize);
        var roster = CleanCharacterIds(characterIds);
        int batchCount = CountBatches(roster.Count, batchSize);
        var items = new List<PrecompileItem>(batchCount);
        for (int i = 0; i < batchCount; i++)
        {
            var slice = Slice(roster, i, batchSize);
            items.Add(new PrecompileItem(PrecompileItemKind.CharacterBattle, CharacterBattleScene,
                $"Troops batch {i + 1}/{batchCount} ({slice.Count} characters)", slice, i, batchCount));
        }
        items.AddRange(ScenePasses(sceneIds));
        return items;
    }

    // The plan the walk starts with at the main menu, where MBObjectManager does not exist yet and the
    // roster cannot be read: one placeholder batch that discovers the roster on load (the runner then
    // re-plans through BuildPlan), followed by the scenes.
    public static IReadOnlyList<PrecompileItem> BuildBootstrapPlan(IEnumerable<string> sceneIds)
    {
        var items = new List<PrecompileItem>
        {
            new PrecompileItem(PrecompileItemKind.CharacterBattle, CharacterBattleScene, "Troops batch 1 (discovering roster)"),
        };
        items.AddRange(ScenePasses(sceneIds));
        return items;
    }

    // The ids batch `batchIndex` holds, sliced exactly as BuildPlan slices them, so the bootstrap
    // battle can take its own share the moment it has the roster.
    public static IReadOnlyList<string> SliceBatch(IEnumerable<string> characterIds, int batchIndex,
        int batchSize = DefaultCharacterBatchSize)
    {
        RequireBatchSize(batchSize);
        if (batchIndex < 0) throw new ArgumentOutOfRangeException(nameof(batchIndex), batchIndex, "batch index is 0-based");
        return Slice(CleanCharacterIds(characterIds), batchIndex, batchSize);
    }

    // Index-based: a 5,000-id roster is sliced in O(batch) per batch, not O(roster).
    private static List<string> Slice(List<string> roster, int batchIndex, int batchSize)
    {
        long start = (long)batchIndex * batchSize;
        if (start >= roster.Count) return new List<string>();
        return roster.GetRange((int)start, Math.Min(batchSize, roster.Count - (int)start));
    }

    // Trim, drop blanks, de-dupe (ordinal: object ids are case-sensitive), first wins, order kept.
    private static List<string> CleanCharacterIds(IEnumerable<string> ids)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var raw in ids ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var id = raw.Trim();
            if (seen.Add(id)) result.Add(id);
        }
        return result;
    }

    private static IEnumerable<PrecompileItem> ScenePasses(IEnumerable<string> sceneIds)
    {
        if (sceneIds == null) yield break;
        foreach (var scene in sceneIds
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Select(s => s.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return new PrecompileItem(PrecompileItemKind.ScenePass, scene, $"Scene: {scene}");
        }
    }

    private static void RequireBatchSize(int batchSize)
    {
        if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "a batch holds at least one character");
    }
}
