using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.CheatMenu.Hooks;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CheatMenu.Cheats;

/// <summary>
/// The console side of the cheat menu (docs/features/cheat-menu.md): each command calls the same <see cref="CheatMenuService"/>
/// method as its menu button and prints the same message. Cheat mode and a running campaign are required (RunInCampaign), the
/// MCM switch is not. Tier B (dev-console.md): each echoes the result. SHAPE IS LOAD-BEARING (MemoryProbeCheats): exactly
/// <c>public static string Name(List&lt;string&gt;)</c>, no static initializer that touches the engine or IoC.
/// </summary>
public static class CheatMenuCheats
{
    private const string GoldUsage = "Usage: taom.cheat_gold [amount 1-1000000, default 10000]";
    private const string InfluenceUsage = "Usage: taom.cheat_influence [amount 1-10000, default 100]";
    private const string RenownUsage = "Usage: taom.cheat_renown [amount 1-10000, default 100]";
    private const string TroopsUsage = "Usage: taom.cheat_troops basic|elite [count 1-500, default 10]";
    private const string HealUsage = "Usage: taom.cheat_heal";
    private const string RelationUsage = "Usage: taom.cheat_relation <kingdom_id> [amount -100..100, default 20]";
    private const string TeleportUsage = "Usage: taom.cheat_teleport <settlement_id> (a town, castle or village)";

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_gold", "taom")]
    public static string CheatGold(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, GoldUsage, args =>
            WithCount(args, 0, CheatMenuMenus.GoldStep, CheatMenuService.MaxGold, GoldUsage, n => Service().GiveGold(n)));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_influence", "taom")]
    public static string CheatInfluence(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, InfluenceUsage, args =>
            WithCount(args, 0, CheatMenuMenus.InfluenceStep, CheatMenuService.MaxInfluenceOrRenown, InfluenceUsage, n => Service().AddInfluence(n)));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_renown", "taom")]
    public static string CheatRenown(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RenownUsage, args =>
            WithCount(args, 0, CheatMenuMenus.RenownStep, CheatMenuService.MaxInfluenceOrRenown, RenownUsage, n => Service().AddRenown(n)));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_troops", "taom")]
    public static string CheatTroops(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, TroopsUsage, args =>
        {
            if (args.Count < 1 || (args[0] != "basic" && args[0] != "elite")) return TroopsUsage;
            var elite = args[0] == "elite";
            return WithCount(args, 1, CheatMenuMenus.TroopStep, CheatMenuService.MaxTroops, TroopsUsage, n => Service().AddTroops(elite, n));
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_heal", "taom")]
    public static string CheatHeal(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, HealUsage, args => args.Count != 0 ? HealUsage : Say(Service().Heal()));

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_relation", "taom")]
    public static string CheatRelation(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RelationUsage, args =>
        {
            if (args.Count < 1) return RelationUsage;
            var kingdom = args[0];
            return WithCount(args, 1, CheatMenuMenus.RelationStep, CheatMenuService.MaxRelation, RelationUsage,
                n => Service().AddRelation(kingdom, n), min: -CheatMenuService.MaxRelation);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("cheat_teleport", "taom")]
    public static string CheatTeleport(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, TeleportUsage, args =>
            args.Count != 1 ? TeleportUsage : Say(Service().Teleport(args[0])));

    private static CheatMenuService Service() => IoC.Resolve<CheatMenuService>();

    // The optional count at args[index]: the default when absent, else parsed within [min, max] (the service checks again).
    private static string WithCount(List<string> args, int index, int fallback, int max, string usage,
        System.Func<int, CheatOutcome> act, int min = 1)
    {
        if (args.Count > index + 1) return usage;
        var n = fallback;
        if (args.Count == index + 1 && !DevConsoleArgs.TryParseCount(args[index], min, max, out n, out var error))
            return error + "\n" + usage;
        return Say(act(n));
    }

    private static string Say(CheatOutcome outcome) => CheatMenuTexts.Describe(outcome).ToString();
}
