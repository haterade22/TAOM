using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.FactionPower.Cheats;

/// <summary>
/// <c>taom.faction_power</c> shows and changes the faction power of each realm culture in this campaign. The work is in
/// <see cref="FactionPowerEditor"/>; this is the thin console entry.
/// </summary>
public static class FactionPowerCheats
{
    private const string Usage =
        "Format is \"taom.faction_power list\", \"taom.faction_power set <culture> <percent>\" or \"taom.faction_power preset <name>\".\n"
        + "list shows every realm culture, its side and its percent (100 is normal).\n"
        + "set changes one realm, 50 to 200 percent. It scales lord party size and garrison size for that realm's AI; the player's own clan is never changed.\n"
        + "preset is standard, evil50 (every evil realm 150), free50 (every free realm 150) or reset (every changed realm back to 100).";

    [CommandLineFunctionality.CommandLineArgumentFunction("faction_power", "taom")]
    public static string FactionPower(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var editor = IoC.Resolve<FactionPowerEditor>();
            if (args.Count == 1 && args[0].ToLowerInvariant() == "list") return editor.List();
            if (args.Count == 3 && args[0].ToLowerInvariant() == "set") return editor.Set(args[1], args[2]);
            if (args.Count == 2 && args[0].ToLowerInvariant() == "preset") return editor.Preset(args[1]);
            return Usage;
        });
}
