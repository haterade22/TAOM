using TAOM.Features.AdvancedCombat;
using TAOM.Features.HeroAbilities.Ranged;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The arrow's kill roll (#1361, decision R3): a landed arrow of a ranged hero asks the one instant-kill executor, on the
/// main tick, one frame after the hit. The executor holds the victim filter (R4, DecideInstantKill) and writes the
/// instant-kill check line. No kill inside a damage model. Boundary code, game-tested (ADR-008).
/// </summary>
internal sealed class HeroArrowKills
{
    // The blow's magnitude, as the melee strikes pass it.
    internal const float Magnitude = 50f;

    private readonly SignatureInstantKill _executor;

    internal HeroArrowKills(SignatureInstantKill executor)
    {
        _executor = executor;
    }

    /// <summary>A registered missile blow of the hero on an enemy. An extra arrow is counted but never rolls.</summary>
    internal void OnBodyHit(in HeroMissileEvent e, HeroRangedTally t)
    {
        var extra = e.IsExtraArrow;
        if (e.BlockedShield || e.BlockedWeapon || !(e.Damage > 0) || e.Victim == null)
            return;
        t.BodyHits++;
        if (e.Headshot)
            t.Head++;
        if (extra)
        {
            t.Exempt++;
            return;
        }

        // CustomAttacksUtils.TakeDamage does nothing for a dead or fading attacker (CustomAttacksUtils.cs:170), so TryKill
        // would write died=0 for a roll it won.
        var hero = e.Hero;
        if (!AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive() || hero.IsFadingOut())
        {
            t.DeadShooter++;
            return;
        }
        // The event drains a tick after the blow, which may have killed him already (Agent.cs:5513, :5529-5532): no call then.
        var chance = HeroRangedService.ArrowKillChance(e.Headshot, extra, e.BlockedShield, e.BlockedWeapon, e.Damage,
            e.Kit.HeadshotKillChance, e.Kit.BodyKillChance, e.Victim.IsActive() && e.Victim.Health > 0f);
        if (!(chance > 0f))
            return;
        t.Calls++;
        if (_executor.TryKill(hero, e.Victim, chance, Magnitude, t.HeroId + "_arrow"))
            t.Kills++;
    }
}
