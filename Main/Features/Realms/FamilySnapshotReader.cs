using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Realms.Domain.Succession;

namespace TAOM.Features.Realms;

/// <summary>
/// Copies the family a succession can reach: the deceased, their descendants, their parents with
/// the parents' descendants (siblings, nephews and nieces), and every member of the deceased's clan.
/// </summary>
internal static class FamilySnapshotReader
{
    private const int MaxDepth = 6;

    public static FamilySnapshot Read(Hero deceased)
    {
        var found = new Dictionary<string, HeroInfo>();
        AddWithDescendants(deceased, found, 0);
        AddWithDescendants(deceased.Father, found, 0);
        AddWithDescendants(deceased.Mother, found, 0);

        var clan = deceased.Clan;
        if (clan != null)
        {
            foreach (var member in clan.Heroes)
                Add(member, found);
        }

        return new FamilySnapshot(found.Values);
    }

    /// <summary>Copies each leader and their children, for clan leaders granting titles.</summary>
    public static FamilySnapshot ReadHouseholds(IEnumerable<Hero> leaders)
    {
        var found = new Dictionary<string, HeroInfo>();
        foreach (var leader in leaders)
        {
            Add(leader, found);
            foreach (var child in leader.Children)
                Add(child, found);
        }

        return new FamilySnapshot(found.Values);
    }

    private static void AddWithDescendants(Hero? hero, Dictionary<string, HeroInfo> found, int depth)
    {
        if (hero == null || depth > MaxDepth || found.ContainsKey(hero.StringId))
            return;
        Add(hero, found);
        foreach (var child in hero.Children)
            AddWithDescendants(child, found, depth + 1);
    }

    private static void Add(Hero? hero, Dictionary<string, HeroInfo> found)
    {
        if (hero == null || found.ContainsKey(hero.StringId))
            return;
        found[hero.StringId] = new HeroInfo(
            hero.StringId,
            hero.IsAlive,
            hero.IsFemale,
            hero.Age,
            hero.Clan?.StringId,
            hero.Father?.StringId,
            hero.Mother?.StringId,
            hero.Children.Where(c => c != null).Select(c => c.StringId).ToList());
    }
}
