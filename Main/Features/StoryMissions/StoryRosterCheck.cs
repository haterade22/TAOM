using System.Collections.Generic;
using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// The verdict of the story roster check (#1366), written once when a campaign session starts: <c>missions=&lt;n&gt; troops=&lt;n&gt; missing=&lt;list or -&gt;
/// items=&lt;n&gt; items_missing=&lt;list or -&gt;</c>. It passes when every troop a story mission can spawn resolves to a character and every hero kit's extra arrow item
/// resolves to an item. A catalog with no mission fails: a lost <c>story_missions.json</c> must never read as a pass. Pure: the service reads the engine.
/// </summary>
internal static class StoryRosterCheck
{
    public const string Id = "taom.story-missions.roster";

    public static CheckVerdict Judge(int missions, int troops, IReadOnlyList<string> missingTroops, int items, IReadOnlyList<string> missingItems) =>
        LongDefeatChecks.Verdict(missions > 0 && missingTroops.Count == 0 && missingItems.Count == 0,
            $"missions={missions} troops={troops} missing={List(missingTroops)} items={items} items_missing={List(missingItems)}");

    private static string List(IReadOnlyList<string> ids) => ids.Count == 0 ? "-" : string.Join(",", ids);
}
