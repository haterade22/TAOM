using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>The ids the local player's hero answers to (his hero id and his character id), for <c>LoreHeroPersons</c>. Empty with no campaign hero. Never throws.</summary>
internal static class LorePlayerIds
{
    public static IReadOnlyCollection<string> Read()
    {
        var ids = new List<string>(2);
        var hero = Campaign.Current == null ? null : Hero.MainHero;
        if (hero?.StringId is { Length: > 0 } heroId)
            ids.Add(heroId);
        if (hero?.CharacterObject?.StringId is { Length: > 0 } characterId)
            ids.Add(characterId);
        return ids;
    }
}
