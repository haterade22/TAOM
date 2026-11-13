using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Laws;

namespace TAOM.Features.Realms.Domain.Succession;

public enum SuccessionRoute
{
    /// <summary>A living descendant: child, grandchild through a dead parent, and so on.</summary>
    Descendant,

    /// <summary>A sibling or a sibling's descendant.</summary>
    Sibling,

    /// <summary>A member of the deceased's clan.</summary>
    Clan,

    /// <summary>Decided later by the game: the clan's next leader.</summary>
    DeferredToClan,

    /// <summary>Decided later by the game: the realm's next ruler.</summary>
    DeferredToRealm,

    /// <summary>Nobody eligible; the title falls vacant.</summary>
    None,
}

public sealed class SuccessionOutcome
{
    public SuccessionOutcome(SuccessionRoute route, string? heirId = null)
    {
        Route = route;
        HeirId = heirId;
    }

    public SuccessionRoute Route { get; }
    public string? HeirId { get; }
}

/// <summary>
/// Picks the heir of one title under its laws. Pure: it reads only the <see cref="FamilySnapshot"/>.
/// Primogeniture searches each child's line in order before the next child, so a dead eldest son's son
/// comes before his younger brother. Ties in age are broken by id so the result is repeatable.
/// </summary>
public static class SuccessionResolver
{
    public const float AdultAge = 18f;

    public static SuccessionOutcome Resolve(string deceasedId, FamilySnapshot family, TitleLaws laws, bool isRealmTitle)
    {
        var deceased = family.Get(deceasedId);
        if (deceased == null)
            return new SuccessionOutcome(SuccessionRoute.None);

        switch (laws.Succession)
        {
            case SuccessionLaw.Elective when isRealmTitle:
                return new SuccessionOutcome(SuccessionRoute.DeferredToRealm);
            case SuccessionLaw.Elective:
            case SuccessionLaw.ClanLeader:
                return deceased.ClanId != null
                    ? new SuccessionOutcome(SuccessionRoute.DeferredToClan)
                    : FamilyLine(deceased, family, laws.Gender, eldestFirst: true);
            case SuccessionLaw.Seniority:
                var senior = ClanCandidates(deceased, family, laws.Gender).FirstOrDefault();
                return senior != null
                    ? new SuccessionOutcome(SuccessionRoute.Clan, senior.Id)
                    : FamilyLine(deceased, family, laws.Gender, eldestFirst: true);
            case SuccessionLaw.Ultimogeniture:
                return FamilyLine(deceased, family, laws.Gender, eldestFirst: false);
            default:
                return FamilyLine(deceased, family, laws.Gender, eldestFirst: true);
        }
    }

    private static SuccessionOutcome FamilyLine(HeroInfo deceased, FamilySnapshot family, GenderLaw gender, bool eldestFirst)
    {
        var visited = new HashSet<string> { deceased.Id };
        var heir = SearchLine(deceased, family, gender, eldestFirst, visited);
        if (heir != null)
            return new SuccessionOutcome(SuccessionRoute.Descendant, heir.Id);

        foreach (var sibling in Order(Siblings(deceased, family), gender, eldestFirst))
        {
            if (!visited.Add(sibling.Id))
                continue;
            if (sibling.IsAlive)
                return new SuccessionOutcome(SuccessionRoute.Sibling, sibling.Id);
            var nephew = SearchLine(sibling, family, gender, eldestFirst, visited);
            if (nephew != null)
                return new SuccessionOutcome(SuccessionRoute.Sibling, nephew.Id);
        }

        var clanHeir = ClanCandidates(deceased, family, gender).FirstOrDefault();
        return clanHeir != null
            ? new SuccessionOutcome(SuccessionRoute.Clan, clanHeir.Id)
            : new SuccessionOutcome(SuccessionRoute.None);
    }

    private static HeroInfo? SearchLine(HeroInfo parent, FamilySnapshot family, GenderLaw gender, bool eldestFirst, HashSet<string> visited)
    {
        var children = parent.ChildIds.Select(family.Get).Where(c => c != null).Select(c => c!);
        foreach (var child in Order(children, gender, eldestFirst))
        {
            if (!visited.Add(child.Id))
                continue;
            if (child.IsAlive)
                return child;
            var descendant = SearchLine(child, family, gender, eldestFirst, visited);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private static IEnumerable<HeroInfo> Siblings(HeroInfo hero, FamilySnapshot family)
    {
        var ids = new HashSet<string>();
        foreach (var parent in new[] { family.Get(hero.FatherId), family.Get(hero.MotherId) })
            if (parent != null)
                ids.UnionWith(parent.ChildIds);
        ids.Remove(hero.Id);
        return ids.Select(family.Get).Where(h => h != null).Select(h => h!);
    }

    /// <summary>Living clan members, adults before children, eldest first.</summary>
    private static IEnumerable<HeroInfo> ClanCandidates(HeroInfo deceased, FamilySnapshot family, GenderLaw gender) =>
        Order(family.ClanMembers(deceased.ClanId).Where(h => h.Id != deceased.Id && h.IsAlive), gender, eldestFirst: true)
            .OrderBy(h => h.Age >= AdultAge ? 0 : 1);

    /// <summary>Drops the ineligible sex under agnatic law, puts men first under male preference, then orders by age.</summary>
    internal static IEnumerable<HeroInfo> Order(IEnumerable<HeroInfo> heroes, GenderLaw gender, bool eldestFirst)
    {
        var eligible = gender == GenderLaw.Agnatic ? heroes.Where(h => !h.IsFemale) : heroes;
        var byPreference = gender == GenderLaw.AgnaticCognatic
            ? eligible.OrderBy(h => h.IsFemale ? 1 : 0)
            : eligible.OrderBy(_ => 0);
        var byAge = eldestFirst ? byPreference.ThenByDescending(h => h.Age) : byPreference.ThenBy(h => h.Age);
        return byAge.ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
    }
}
