using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.print_battle_scene` — which battle terrain a fight at the party's current map position
/// would actually load.
///
/// Why it earns its place: vanilla renames and removes battle scenes between engine versions, and
/// TAOM's map data references them by index. A position whose map patch index no scene declares
/// produces a fallback or a failed assert at battle start — a class of breakage that an engine bump
/// introduces silently and that `.claude/rules/vanilla-data-comparison.md` exists to catch. Vanilla's
/// own `campaign.print_player_party_position` prints coordinates and stops short of the scene lookup.
/// </summary>
public static class MapSceneCheats
{
    private const string Usage =
        "Format is \"taom.print_battle_scene\".\n"
        + "Prints the party's map position, the map patch sceneIndex, and every sp_battle_scenes\n"
        + "entry whose map_indices contain it — i.e. the battle terrain a fight here would load.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_battle_scene", "taom")]
    public static string PrintBattleScene(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var party = MobileParty.MainParty;
            if (party == null) return "No main party.";

            var mapScene = Campaign.Current?.MapSceneWrapper;
            if (mapScene == null) return "The campaign map scene is not loaded.";

            var position = party.Position;
            var patch = mapScene.GetMapPatchAtPosition(position);

            return MissionReportFormatter.FormatBattleScene(new BattleSceneQuery
            {
                X = position.X,
                Y = position.Y,
                MapIndex = patch.sceneIndex,
                CandidateSceneIds = FindCandidates(patch.sceneIndex),
            });
        });

    private static IReadOnlyList<string> FindCandidates(int sceneIndex)
    {
        var manager = GameSceneDataManager.Instance;
        var scenes = manager?.SingleplayerBattleScenes;
        if (scenes == null) return new string[0];

        return scenes
            .Where(s => s.MapIndices != null && s.MapIndices.Contains(sceneIndex))
            .Select(s => s.SceneID)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
    }
}
