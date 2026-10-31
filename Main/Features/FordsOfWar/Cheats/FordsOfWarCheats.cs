using System.Collections.Generic;
using System;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Cheats;

/// <summary>
/// `taom.fords_start [westFaction] [eastFaction] [aiProfile] [scene]` starts a Fords of War match from the main menu through
/// the same path the menu prompts end in, with no prompts; `taom.fords_gold &lt;amount&gt;` adds gold to the human commander
/// during a match. Both only route to <see cref="FordsOfWarCommands"/>; the services come from the container because a static
/// console method has no constructor to inject through (docs/features/dev-console.md).
/// </summary>
public static class FordsOfWarCheats
{
    private const string StartUsage =
        "Format is \"taom.fords_start [westFaction] [eastFaction] [aiProfile] [scene]\".\n"
        + "Starts a Fords of War match from the main menu with no prompts. Every argument is optional:\n"
        + "west is the first faction, east the first other faction, the profile normal, the scene the first map.\n"
        + "Example: taom.fords_start gondor mordor hard";

    private const string GoldUsage =
        "Format is \"taom.fords_gold <amount>\".\n"
        + "Adds gold (1 to 99,999) to your commander during a Fords of War match. The purse stops at its cap.";

    [CommandLineFunctionality.CommandLineArgumentFunction("fords_start", "taom")]
    public static string FordsStart(List<string> strings)
    {
        var ran = false;
        var reply = TaomConsole.RunAtMainMenu(strings, StartUsage, args =>
        {
            ran = true;
            return FordsOfWarCommands.Start(args, IoC.Resolve<FordsConfigCatalog>(), IoC.Resolve<FordsSetupPrompt>());
        });
        Record("fords_start", strings, ran, reply);
        return reply;
    }

    [CommandLineFunctionality.CommandLineArgumentFunction("fords_gold", "taom")]
    public static string FordsGold(List<string> strings)
    {
        var ran = false;
        var reply = TaomConsole.RunInMission(strings, GoldUsage, args =>
        {
            ran = true;
            return FordsOfWarCommands.Gold(args, IoC.Resolve<FordsSession>());
        });
        Record("fords_gold", strings, ran, reply);
        return reply;
    }

    // The run report keeps the console call as evidence; it is never allowed to change the reply or throw into the console.
    private static void Record(string command, List<string> args, bool ran, string reply)
    {
        try
        {
            IoC.Resolve<FordsRunReport>().ConsoleCommand(command, args, ran, reply);
        }
        catch (Exception)
        {
            // No container yet (a test host) or a faulted report: the command itself already ran.
        }
    }
}
