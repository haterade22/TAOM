using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Cheats;

/// <summary>
/// <c>taom.tactic_status</c>: the doctrine status line for every AI team, on demand. The same
/// text the debug toggle writes every 5 s, so an A/B run can be spot-checked without the toggle.
///
/// SHAPE IS LOAD-BEARING: a <c>[CommandLineArgumentFunction]</c> whose signature is not exactly
/// <c>public static string Name(List&lt;string&gt;)</c> throws inside the engine's unguarded discovery
/// loop (docs/features/dev-console.md). No static field initializers, every resolve inside the
/// lambda. Pinned by ConsoleCommandBindingTests.
/// </summary>
public static class CultureDoctrineCheats
{
    private const string Usage =
        "Format is \"taom.tactic_status\".\n"
        + "Prints, for every team with a team AI, its current tactic and each formation's active behaviour and arrangement.\n"
        + "Works in Custom Battle. The same line is written to taom_debug every 5 s when Culture Doctrine Debug is on.";

    [CommandLineFunctionality.CommandLineArgumentFunction("tactic_status", "taom")]
    public static string TacticStatus(List<string> strings) =>
        DevConsole.TaomConsole.RunInMission(strings, Usage, _ =>
        {
            var mission = Mission.Current;
            return mission == null ? "No mission is running." : TeamTacticProbe.DescribeAll(mission);
        });
}
