using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.CheatMenu.Battle;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CheatMenu.Cheats;

/// <summary>
/// The console side of the battle cheat menu (docs/features/cheat-menu.md, "Battle"): each command calls the same
/// <see cref="BattleCheatService"/> method as its list entry and prints the same message. Cheat mode and a mission are required
/// (RunInMission), the MCM switch is not, as for the campaign commands. Tier B (dev-console.md): each echoes the result.
/// SHAPE IS LOAD-BEARING (MemoryProbeCheats): exactly <c>public static string Name(List&lt;string&gt;)</c>, no static initializer
/// that touches the engine or IoC.
/// </summary>
public static class BattleCheatCheats
{
    private const string HealUsage = "Usage: taom.cheat_battle_heal [self|side, default self]";
    private const string GodUsage = "Usage: taom.cheat_battle_god [on|off, default: switch it]";
    private const string RoutUsage = "Usage: taom.cheat_battle_rout";
    private const string KillUsage = "Usage: taom.cheat_battle_kill";
    private const string SpawnUsage = "Usage: taom.cheat_battle_spawn <count 1-200> [ally|enemy, default enemy]";

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_battle_heal", "taom")]
    public static string CheatBattleHeal(List<string> strings) =>
        TaomConsole.RunInMission(strings, HealUsage, args =>
        {
            if (args.Count > 1) return HealUsage;
            var which = args.Count == 0 ? "self" : args[0].Trim().ToLowerInvariant();
            if (which == "self") return Say(BattleCheatAction.HealSelf, Service().HealSelf());
            if (which == "side") return Say(BattleCheatAction.HealSide, Service().HealSide());
            return HealUsage;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_battle_god", "taom")]
    public static string CheatBattleGod(List<string> strings) =>
        TaomConsole.RunInMission(strings, GodUsage, args =>
        {
            if (args.Count > 1) return GodUsage;
            if (args.Count == 0) return Say(BattleCheatAction.GodMode, Service().ToggleGodMode());
            var which = args[0].Trim().ToLowerInvariant();
            if (which == "on") return Say(BattleCheatAction.GodMode, Service().SetGodMode(true));
            if (which == "off") return Say(BattleCheatAction.GodMode, Service().SetGodMode(false));
            return GodUsage;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_battle_rout", "taom")]
    public static string CheatBattleRout(List<string> strings) =>
        TaomConsole.RunInMission(strings, RoutUsage, args =>
            args.Count != 0 ? RoutUsage : Say(BattleCheatAction.Rout, Service().RoutEnemies()));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_battle_kill", "taom")]
    public static string CheatBattleKill(List<string> strings) =>
        TaomConsole.RunInMission(strings, KillUsage, args =>
            args.Count != 0 ? KillUsage : Say(BattleCheatAction.Kill, Service().KillEnemies()));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_battle_spawn", "taom")]
    public static string CheatBattleSpawn(List<string> strings) =>
        TaomConsole.RunInMission(strings, SpawnUsage, args =>
        {
            if (args.Count < 1 || args.Count > 2) return SpawnUsage;
            if (!DevConsoleArgs.TryParseCount(args[0], 1, BattleCheatService.MaxSpawn, out var count, out var countError))
                return countError + "\n" + SpawnUsage;
            if (!DevConsoleArgs.TryParseSide(args.Count > 1 ? args[1] : null, out var ally, out var sideError))
                return sideError + "\n" + SpawnUsage;
            return Say(ally ? BattleCheatAction.SpawnAllies : BattleCheatAction.SpawnEnemies, Service().SpawnTroops(ally, count));
        });

    private static BattleCheatService Service() => IoC.Resolve<BattleCheatService>();

    private static string Say(BattleCheatAction action, CheatOutcome outcome) => BattleCheatTexts.Describe(action, outcome).ToString();
}
