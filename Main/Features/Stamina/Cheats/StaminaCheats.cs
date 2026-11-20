using System.Collections.Generic;
using TAOM.Features.Stamina.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Cheats;

/// <summary>
/// <c>taom.set_stamina</c> and <c>taom.print_stamina</c> (#1497): put soldiers of the battle on screen into a stamina
/// band and read back what stamina is doing. They are how the engine spikes of the feature are run, in a Custom Battle
/// with <c>taom.spawn_troops</c>. A forced band stays until "auto" gives the soldier back to the rules.
///
/// SHAPE IS LOAD-BEARING: a <c>[CommandLineArgumentFunction]</c> whose signature is not exactly
/// <c>public static string Name(List&lt;string&gt;)</c> throws inside the engine's unguarded discovery loop
/// (docs/features/dev-console.md). No static field initializers, every resolve inside the lambda. Pinned by
/// ConsoleCommandBindingTests.
/// </summary>
public static class StaminaCheats
{
    private const string SetUsage =
        "Format is \"taom.set_stamina [fresh|winded|tired|exhausted|auto] [player|allies|enemies|all]\".\n"
        + "Puts the chosen soldiers into a stamina band; \"auto\" gives them back to the stamina rules.\n"
        + "Only soldiers this game owns are changed, never horses or other mounts. Needs Enable Combat Stamina and a field battle or siege.\n"
        + "Works in Custom Battle.";

    private const string PrintUsage =
        "Format is \"taom.print_stamina\".\n"
        + "Prints, for the battle on screen, the stamina mission gate, how many soldiers are in each band, how many are locked, and the counters.\n"
        + "Works in Custom Battle.";

    [CommandLineFunctionality.CommandLineArgumentFunction("set_stamina", "taom")]
    public static string SetStamina(List<string> strings) =>
        DevConsole.TaomConsole.RunInMission(strings, SetUsage, args =>
        {
            if (Mission.Current == null)
                return "No mission is running.";
            if (!StaminaCheatArgs.TryParse(args, out var band, out var target, out var error))
                return error + "\n" + SetUsage;
            return IoC.Resolve<StaminaRuntime>().Ticker.Force(band, target);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("print_stamina", "taom")]
    public static string PrintStamina(List<string> strings) =>
        DevConsole.TaomConsole.RunInMission(strings, PrintUsage, _ =>
            Mission.Current == null ? "No mission is running." : IoC.Resolve<StaminaRuntime>().Ticker.Describe());
}
