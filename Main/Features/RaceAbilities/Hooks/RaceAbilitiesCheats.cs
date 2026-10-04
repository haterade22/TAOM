using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// <c>taom.race_abilities</c>: whether the race abilities are working in the battle on screen. Prints the
/// mission gate, the soldiers carrying an ability tree, who is live right now, and the battle's counters
/// (waves, soldiers, what fired them, crush-throughs forced and held, shrug-offs, damage added and
/// prevented, kills, heals, fear). The same report is written to the log every 30 s of activity and at
/// mission end.
///
/// SHAPE IS LOAD-BEARING: a <c>[CommandLineArgumentFunction]</c> whose signature is not exactly
/// <c>public static string Name(List&lt;string&gt;)</c> throws inside the engine's unguarded discovery
/// loop (docs/features/dev-console.md). No static field initializers, every resolve inside the
/// lambda. Pinned by ConsoleCommandBindingTests.
/// </summary>
public static class RaceAbilitiesCheats
{
    private const string Usage =
        "Format is \"taom.race_abilities\".\n"
        + "Prints, for the battle on screen, how many soldiers carry a race ability, how many are live now, and what each ability has done.\n"
        + "Works in Custom Battle.";

    [CommandLineFunctionality.CommandLineArgumentFunction("race_abilities", "taom")]
    public static string RaceAbilities(List<string> strings) =>
        DevConsole.TaomConsole.RunInMission(strings, Usage, _ =>
            Mission.Current == null ? "No mission is running." : IoC.Resolve<RaceAbilityRuntime>().DescribeStatus());
}
