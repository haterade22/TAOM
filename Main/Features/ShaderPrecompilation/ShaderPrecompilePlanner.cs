using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.ShaderPrecompilation.Domain;

namespace TAOM.Features.ShaderPrecompilation;

// Pure builder for the ordered precompile work list. Item 0 is the all-characters battle
// (character/equipment shaders); the rest are one ScenePass per battle scene (terrain +
// forced-atmosphere shaders — the #287 class). Order: characters first (the bulk), then scenes.
public static class ShaderPrecompilePlanner
{
    // The all-characters battle runs on the always-present default custom-battle scene.
    public const string CharacterBattleScene = "battle_terrain_029";

    public static IReadOnlyList<PrecompileItem> BuildPlan(IEnumerable<string> sceneIds)
    {
        var items = new List<PrecompileItem>
        {
            new PrecompileItem(PrecompileItemKind.CharacterBattle, CharacterBattleScene,
                "All troops — character & equipment shaders"),
        };

        if (sceneIds != null)
        {
            foreach (var scene in sceneIds
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .Select(s => s.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                items.Add(new PrecompileItem(PrecompileItemKind.ScenePass, scene, $"Scene — {scene}"));
            }
        }

        return items;
    }
}
