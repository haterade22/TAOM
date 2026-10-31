using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

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
    public static string FordsStart(List<string> strings) =>
        TaomConsole.RunAtMainMenu(strings, StartUsage, args =>
            FordsOfWarCommands.Start(args, IoC.Resolve<FordsConfigCatalog>(), IoC.Resolve<FordsSetupPrompt>()));

    [CommandLineFunctionality.CommandLineArgumentFunction("fords_gold", "taom")]
    public static string FordsGold(List<string> strings) =>
        TaomConsole.RunInMission(strings, GoldUsage, args => FordsOfWarCommands.Gold(args, IoC.Resolve<FordsSession>()));
}
