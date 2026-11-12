using System.Collections.Generic;

namespace TAOM.Features.LordRetinue;

/// <summary>
/// Pure decision for LordRetinue (ADR-002/007): whether an AI lord party takes one more clan hero, which
/// idle hero it takes and which empty party role that hero fills, and when it gives its retinue back.
/// </summary>
public sealed class LordRetinueService
{
    private static readonly RetinueRole[] Roles =
    {
        RetinueRole.Scout, RetinueRole.Surgeon, RetinueRole.Engineer, RetinueRole.Quartermaster,
    };

    private const RetinueRole AllRoles =
        RetinueRole.Scout | RetinueRole.Surgeon | RetinueRole.Engineer | RetinueRole.Quartermaster;

    private readonly ILordRetinueSettingsProvider _settings;

    public LordRetinueService(ILordRetinueSettingsProvider settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// A party takes a hero only while its clan sits at its party limit. Below the limit vanilla's
    /// <c>GetBestAvailableCommander</c> turns any idle adult lord of the clan into a party leader, and a
    /// hero already aboard is never a candidate for that.
    /// </summary>
    public bool ShouldEvaluate(RetinueParty party)
    {
        if (party == null || !_settings.IsEnabled || party.ClanBelowPartyLimit)
            return false;
        if (party.ExtraHeroes >= _settings.MaxExtraHeroes)
            return false;
        return (party.FilledRoles & AllRoles) != AllRoles;
    }

    /// <summary>The retinue goes back to its settlement when vanilla wants the heroes as leaders again, or the feature is off.</summary>
    public bool ShouldRelease(RetinueParty party)
        => party != null && party.RetinueHeroIds.Count > 0 && (!_settings.IsEnabled || party.ClanBelowPartyLimit);

    public bool IsEligible(RetinueHero hero)
        => hero != null
           && hero.InPartySettlement && hero.IsActive && hero.IsAlive && hero.IsAdult
           && hero.CanMoveToSettlement && hero.CanHavePartyRole
           && !hero.HasParty && !hero.IsPrisoner && !hero.IsPlayerControlled && !hero.IsTemplate
           && !hero.IsNotable && !hero.IsWanderer && !hero.IsWounded && !hero.IsNoncombatant
           && !hero.IsGovernor && !hero.IsClanLeader;

    /// <summary>
    /// The eligible hero and empty role with the largest skill gain over the leader, who already fills every
    /// empty role. A hero who is no better than the leader at any empty role is not taken. Ties keep the first.
    /// </summary>
    public RetinuePick? Choose(RetinueParty party, IReadOnlyList<RetinueHero> heroes)
    {
        if (party == null || heroes == null)
            return null;

        RetinuePick? best = null;
        var bestGain = 0;
        foreach (var hero in heroes)
        {
            if (!IsEligible(hero))
                continue;

            foreach (var role in Roles)
            {
                if ((party.FilledRoles & role) != 0)
                    continue;
                var gain = hero.Skills.For(role) - party.LeaderSkills.For(role);
                if (gain <= bestGain)
                    continue;
                best = new RetinuePick(hero.HeroId, role);
                bestGain = gain;
            }
        }

        return best;
    }
}
