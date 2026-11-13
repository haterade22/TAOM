using System;
using System.Collections.Generic;

namespace TAOM.Features.LordRetinue;

[Flags]
public enum RetinueRole
{
    None = 0,
    Scout = 1,
    Surgeon = 2,
    Engineer = 4,
    Quartermaster = 8,
}

/// <summary>The four skills that drive the four party roles, read once from a hero.</summary>
public sealed class RetinueSkills
{
    public RetinueSkills(int scouting, int medicine, int engineering, int steward)
    {
        Scouting = scouting;
        Medicine = medicine;
        Engineering = engineering;
        Steward = steward;
    }

    public int Scouting { get; }

    public int Medicine { get; }

    public int Engineering { get; }

    public int Steward { get; }

    public int For(RetinueRole role) => role switch
    {
        RetinueRole.Scout => Scouting,
        RetinueRole.Surgeon => Medicine,
        RetinueRole.Engineer => Engineering,
        RetinueRole.Quartermaster => Steward,
        _ => 0,
    };
}

/// <summary>Engine-free snapshot of one AI lord party that sits in a settlement.</summary>
public sealed class RetinueParty
{
    public RetinueParty(
        int extraHeroes, bool clanBelowPartyLimit, RetinueRole filledRoles,
        RetinueSkills leaderSkills, IReadOnlyList<string> retinueHeroIds)
    {
        ExtraHeroes = extraHeroes;
        ClanBelowPartyLimit = clanBelowPartyLimit;
        FilledRoles = filledRoles;
        LeaderSkills = leaderSkills;
        RetinueHeroIds = retinueHeroIds;
    }

    /// <summary>Heroes aboard besides the leader, whoever put them there.</summary>
    public int ExtraHeroes { get; }

    /// <summary>True while vanilla may still spawn a party for this clan from an idle hero.</summary>
    public bool ClanBelowPartyLimit { get; }

    /// <summary>Roles that already have a holder aboard.</summary>
    public RetinueRole FilledRoles { get; }

    /// <summary>The leader fills every empty role, so a role only improves when a hero beats these values.</summary>
    public RetinueSkills LeaderSkills { get; }

    /// <summary>Same-clan non-leader heroes that hold a party role here: the heroes this feature put aboard.</summary>
    public IReadOnlyList<string> RetinueHeroIds { get; }
}

/// <summary>Engine-free facts about one clan hero; <see cref="LordRetinueService.IsEligible"/> decides.</summary>
public sealed class RetinueHero
{
    public RetinueHero(string heroId, RetinueSkills skills)
    {
        HeroId = heroId;
        Skills = skills;
    }

    public string HeroId { get; }

    public RetinueSkills Skills { get; }

    /// <summary>Stands in the settlement the party sits in.</summary>
    public bool InPartySettlement { get; set; }

    public bool IsActive { get; set; }

    public bool IsAlive { get; set; }

    public bool IsAdult { get; set; }

    /// <summary>False while a quest, issue or other listener vetoes moving the hero (<c>Hero.CanMoveToSettlement</c>).</summary>
    public bool CanMoveToSettlement { get; set; }

    /// <summary>The check vanilla makes before it gives a hero a party role (<c>Hero.CanBeGovernorOrHavePartyRole</c>).</summary>
    public bool CanHavePartyRole { get; set; }

    /// <summary>Already belongs to a party.</summary>
    public bool HasParty { get; set; }

    public bool IsPrisoner { get; set; }

    /// <summary>A human player drives the hero, or the co-op registry cannot say.</summary>
    public bool IsPlayerControlled { get; set; }

    public bool IsTemplate { get; set; }

    public bool IsNotable { get; set; }

    public bool IsWanderer { get; set; }

    public bool IsWounded { get; set; }

    public bool IsNoncombatant { get; set; }

    public bool IsGovernor { get; set; }

    public bool IsClanLeader { get; set; }
}

public sealed class RetinuePick
{
    public RetinuePick(string heroId, RetinueRole role)
    {
        HeroId = heroId;
        Role = role;
    }

    public string HeroId { get; }

    public RetinueRole Role { get; }
}
