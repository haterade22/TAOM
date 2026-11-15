using System.Collections.Generic;
using TaleWorlds.Library;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.spawn_troops <troopId> <count> [enemy|ally]` — put troops into the CURRENT mission.
///
/// The single largest gap in vanilla's console: it ships ~80 campaign cheats and 10 mission cheats
/// and not one of them spawns anything. Composing a specific fight today means Custom Battle (which
/// gives you a culture's default roster, not "20 dwarves and one mûmakil") or engineering the
/// campaign into the right encounter.
///
/// Uses the mission gate rather than the campaign gate, so it works in a custom battle — TAOM's main
/// venue for testing creatures, mounts and equipment. The spawning itself is
/// <see cref="MissionTroopSpawner"/>, shared with the battle cheat menu.
/// </summary>
public static class MissionSpawnCheats
{
    private const string Usage =
        "Format is \"taom.spawn_troops [troopId] [count] [enemy|ally]\".\n"
        + "Spawns count troops into the current mission near you. Side defaults to enemy.\n"
        + "Count is clamped to [1,200]. Example: taom.spawn_troops taom_troll 5 enemy";

    [CommandLineFunctionality.CommandLineArgumentFunction("spawn_troops", "taom")]
    public static string SpawnTroops(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count < 2) return "Expected a troop id and a count.\n" + Usage;
            if (!DevConsoleArgs.TryParseCount(args[1], 1, MissionTroopSpawner.MaxCount, out var count, out var countError))
                return countError + "\n" + Usage;
            if (!DevConsoleArgs.TryParseSide(args.Count > 2 ? args[2] : null, out var isPlayerSide, out var sideError))
                return sideError + "\n" + Usage;

            return MissionReportFormatter.FormatSpawn(MissionTroopSpawner.Spawn(args[0], count, isPlayerSide));
        });
}
