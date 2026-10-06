using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.MapEventGuard.Cheats;

/// <summary>
/// <c>taom.print_stuck_battles</c> (tier A): every live map event with its healthy counts, age, attached destroyed
/// parties and the stuck-battle verdict. Changes nothing; the hourly sweep does the work.
/// </summary>
public static class StuckBattleCheats
{
    private const string Usage = "Format is \"taom.print_stuck_battles\". No arguments.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_stuck_battles", "taom")]
    public static string PrintStuckBattles(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var service = IoC.Resolve<StuckBattleService>();
            var lines = service.Describe();
            string body = lines.Count == 0 ? "No readable map events." : string.Join("\n", lines);
            return service.IsStandingDown
                ? "Co-op session: the hourly sweep is standing down and acts on none of these.\n" + body
                : body;
        });
}
