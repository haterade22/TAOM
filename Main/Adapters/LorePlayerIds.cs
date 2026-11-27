using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>The ids the local player's hero answers to (the hero id and the character id), for <c>LoreHeroPersons</c>. Empty with no campaign hero. Never throws.</summary>
internal static class LorePlayerIds
{
    public static IReadOnlyCollection<string> Read()
    {
        var ids = new List<string>(2);
        var hero = Campaign.Current == null || !(Game.Current?.PlayerTroop is CharacterObject) ? null : Hero.MainHero;
        if (hero?.StringId is { Length: > 0 } heroId)
            ids.Add(heroId);
        if (hero?.CharacterObject?.StringId is { Length: > 0 } characterId)
            ids.Add(characterId);
        return ids;
    }
}
