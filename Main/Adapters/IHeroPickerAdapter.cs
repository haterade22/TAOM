using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Facts about the heroes a player could take over. Facts only, no decisions: every eligibility
/// rule lives in HeroPickerService so it can be unit tested without a running campaign.
/// </summary>
public interface IHeroPickerAdapter
{
    /// <summary>
    /// One pass over the live campaign. Returns every hero that could conceivably be offered,
    /// tagged with the relationships the grouping rules need.
    /// </summary>
    IReadOnlyList<PickableHeroInfo> GetCandidates(string cultureId);
}

/// <summary>
/// A hero as the picker sees them. Wide by design: it is a data carrier, and every field here
/// exists because a grouping or filtering rule reads it.
/// </summary>
public readonly struct PickableHeroInfo
{
    public PickableHeroInfo(
        string heroId,
        string name,
        string cultureId,
        string clanId,
        int race,
        bool isFemale,
        bool isChild,
        bool isWanderer,
        bool isNotable,
        bool isMainHero,
        bool isClanLeader,
        bool isKingdomLeader,
        bool isSpouseOfKingdomLeader,
        bool isChildOfKingdomLeader,
        bool isLoreLocked)
    {
        HeroId = heroId;
        Name = name;
        CultureId = cultureId;
        ClanId = clanId;
        Race = race;
        IsFemale = isFemale;
        IsChild = isChild;
        IsWanderer = isWanderer;
        IsNotable = isNotable;
        IsMainHero = isMainHero;
        IsClanLeader = isClanLeader;
        IsKingdomLeader = isKingdomLeader;
        IsSpouseOfKingdomLeader = isSpouseOfKingdomLeader;
        IsChildOfKingdomLeader = isChildOfKingdomLeader;
        IsLoreLocked = isLoreLocked;
    }

    public string HeroId { get; }
    public string Name { get; }
    public string CultureId { get; }

    /// <summary>Empty when the hero belongs to no clan.</summary>
    public string ClanId { get; }

    public int Race { get; }
    public bool IsFemale { get; }
    public bool IsChild { get; }
    public bool IsWanderer { get; }
    public bool IsNotable { get; }
    public bool IsMainHero { get; }
    public bool IsClanLeader { get; }
    public bool IsKingdomLeader { get; }
    public bool IsSpouseOfKingdomLeader { get; }
    public bool IsChildOfKingdomLeader { get; }

    /// <summary>
    /// Sauron and the Nine, per IUncapturableRegistry. A fact about TAOM's data; whether to
    /// offer them is policy and lives in the service.
    /// </summary>
    public bool IsLoreLocked { get; }
}
