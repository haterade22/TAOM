using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IAllegianceRelationAdapter"/>. <see cref="ReadLords"/> takes the lords vanilla's
/// <c>CharacterRelationCampaignBehavior.UpdateFriendshipAndEnemies</c> takes (v1.5.7: from both <c>AliveHeroes</c> and
/// <c>DeadOrDisabledHeroes</c>, a lord, not the main hero, with a map faction whose leader is not the main hero). Dead lords count:
/// a dead lord's encyclopedia page lists the living lords they are friends with. The engine stores one value per pair
/// (<c>CharacterRelationManager.HeroRelations</c>), so one write per pair is enough. Main thread, once per new game. The list is
/// held between calls of one run and dropped by <see cref="Release"/>, because this adapter is a process singleton.
/// </summary>
public sealed class AllegianceRelationAdapter : IAllegianceRelationAdapter
{
    private List<Hero> _lords = new();

    public IReadOnlyList<SideIds> ReadLords()
    {
        _lords = new List<Hero>();
        var sides = new List<SideIds>();
        var campaign = Campaign.Current;
        if (campaign == null)
            return sides;
        var main = Hero.MainHero;
        Add(campaign.AliveHeroes, main, sides);
        Add(campaign.DeadOrDisabledHeroes, main, sides);
        return sides;
    }

    private void Add(IEnumerable<Hero> heroes, Hero main, List<SideIds> sides)
    {
        foreach (var hero in heroes)
        {
            if (hero == null || !hero.IsLord || hero == main)
                continue;
            var faction = hero.MapFaction;
            if (faction == null || faction.Leader == main)
                continue;
            _lords.Add(hero);
            sides.Add(FactionSides.OfOwnClan(hero));
        }
    }

    public int ReadRelation(int first, int second) => CharacterRelationManager.GetHeroRelation(_lords[first], _lords[second]);

    public void WriteRelation(int first, int second, int value) => _lords[first].SetPersonalRelation(_lords[second], value);

    public void Release() => _lords = new List<Hero>();
}
