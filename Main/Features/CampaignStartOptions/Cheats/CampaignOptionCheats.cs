using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CampaignStartOptions.Cheats;

/// <summary>
/// `taom.campaign_option` shows and changes the start choices this campaign pinned (War of the Ring, its days, Troop
/// Weight, War can be won, Eye of Sauron hunters, Large AI armies, Alignment rules). The work is in
/// <see cref="CampaignOptionEditor"/>; this is the thin console entry.
/// </summary>
public static class CampaignOptionCheats
{
    private const string Usage =
        "Format is \"taom.campaign_option list\" or \"taom.campaign_option set <name> <value>\".\n"
        + "list shows this campaign's start choices next to the MCM value now.\n"
        + "set changes one of them for this campaign only (on or off; days 1 to 365; alignment standard, strict or relaxed).\n"
        + "Options: war_of_the_ring, war_day1, war_day2, troop_weight, war_can_be_won, eye_hunters, large_ai_armies, alignment.";

    [CommandLineFunctionality.CommandLineArgumentFunction("campaign_option", "taom")]
    public static string CampaignOption(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var editor = IoC.Resolve<CampaignOptionEditor>();
            if (args.Count == 1 && args[0].ToLowerInvariant() == "list") return editor.List();
            if (args.Count == 3 && args[0].ToLowerInvariant() == "set") return editor.Set(args[1], args[2]);
            return Usage;
        });
}
