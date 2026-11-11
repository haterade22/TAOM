using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Features.StoryMissions.Hooks;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// <c>taom.story_open &lt;mission&gt; [auto]</c> (#1105): opens the story mission's dev menu on the campaign map, where one click opens the mission.
/// It only routes to <see cref="StoryDevMenuBehavior"/>, which the engine hands out as a campaign behavior because a static console method has
/// no constructor to inject through (docs/features/dev-console.md).
/// </summary>
public static class StoryMissionCheats
{
    private const string Usage = "Format is \"taom.story_open <mission> [auto]\". Opens a story mission from a menu on the campaign map; auto lets the bot play the lord.";

    [CommandLineFunctionality.CommandLineArgumentFunction("story_open", "taom")]
    public static string StoryOpen(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            if (!TryParse(args, out var id, out var auto, out var error))
                return error;

            var behavior = Campaign.Current?.GetCampaignBehavior<StoryDevMenuBehavior>();
            return behavior == null ? "The story menu is not loaded in this campaign." : behavior.Open(id, auto);
        });

    internal static bool TryParse(List<string>? args, out string id, out bool auto, out string error)
    {
        id = "";
        auto = false;
        error = Usage;
        if (args == null || args.Count < 1 || args.Count > 2 || string.IsNullOrWhiteSpace(args[0]))
            return false;
        if (args.Count == 2)
        {
            if (args[1] != "auto")
            {
                error = $"Unknown word '{args[1]}'. " + Usage;
                return false;
            }

            auto = true;
        }

        id = args[0].Trim();
        error = "";
        return true;
    }
}
