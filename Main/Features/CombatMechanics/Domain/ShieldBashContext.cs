using System;

namespace TAOM.Features.CombatMechanics.Domain;

// Boundary DTO for the shield-bash and kick knockdown decision (ADR-007). Built by CombatMechanicsHooks from the sealed
// engine params; the service never sees Agent or WeaponComponentData. A bash is an alternative attack with a weapon, a kick
// the same with none. The ShrugOff blow flag is deliberately not an input: vanilla sets it on nearly every bash and kick
// (1 to 3 damage against a blunt stagger threshold of 5), so VictimIsImmune carries the three deliberate immunities instead.
public readonly struct ShieldBashContext
{
    public readonly bool IsAlternativeAttack;
    public readonly bool IsHorseCharge;

    // Kick: the alternative attack of an empty hand (Mission.CreateMeleeBlow types it AgentAttackType.Kick).
    public readonly bool AttackerWeaponIsNull;

    // The attacker's usage item is a SmallShield or LargeShield.
    public readonly bool AttackerWeaponIsShield;
    public readonly bool AttackerIsHuman;

    // Creature stagger immunity, a live race ability that shrugs off blows, or the hero career shrug-off passive.
    public readonly bool VictimIsImmune;

    // The raw Athletics skill of each side through the registered AgentStatCalculateModel; no perk or race ability
    // touches Athletics in v1.5.5.
    public readonly int AttackerAthletics;
    public readonly int VictimAthletics;

    // Null when the character is unavailable. Never default to 0: race id 0 is a real race.
    public readonly int? VictimRaceId;

    // A live race ability's knockdown resistance scale (1 with none; +100% gives 2). The service counts a
    // non-finite or non-positive value as 1.
    public readonly float VictimAbilityResistanceFactor;

    // The career TroopKnockdownResistance counter (#769 E10) as a scale on that factor, or null. The service calls it only
    // once every gate has passed, because the call counts the troop for the mission-end [CareerPerks] summary.
    public readonly Func<float, float>? VictimCareerResistanceScale;

    // The agent the player controls now (after a takeover, the soldier taken over).
    public readonly bool VictimIsPlayer;

    // Captured at the boundary (MBRandom.RandomFloat, 0 to 1) so the service stays deterministic.
    public readonly float RandomRoll;

    public ShieldBashContext(
        bool isAlternativeAttack,
        bool isHorseCharge,
        bool attackerWeaponIsNull,
        bool attackerWeaponIsShield,
        bool attackerIsHuman,
        bool victimIsImmune,
        int attackerAthletics,
        int victimAthletics,
        int? victimRaceId,
        float victimAbilityResistanceFactor,
        bool victimIsPlayer,
        float randomRoll,
        Func<float, float>? victimCareerResistanceScale = null)
    {
        IsAlternativeAttack = isAlternativeAttack;
        IsHorseCharge = isHorseCharge;
        AttackerWeaponIsNull = attackerWeaponIsNull;
        AttackerWeaponIsShield = attackerWeaponIsShield;
        AttackerIsHuman = attackerIsHuman;
        VictimIsImmune = victimIsImmune;
        AttackerAthletics = attackerAthletics;
        VictimAthletics = victimAthletics;
        VictimRaceId = victimRaceId;
        VictimAbilityResistanceFactor = victimAbilityResistanceFactor;
        VictimIsPlayer = victimIsPlayer;
        RandomRoll = randomRoll;
        VictimCareerResistanceScale = victimCareerResistanceScale;
    }
}
