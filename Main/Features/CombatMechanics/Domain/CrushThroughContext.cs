namespace TAOM.Features.CombatMechanics.Domain;

// Boundary DTO for the crush-through decision. Built by TaomCombatMechanicsModel from the sealed
// engine params (ADR-007); the service never sees Agent/WeaponComponentData.
public readonly struct CrushThroughContext
{
    public readonly float TotalAttackEnergy;
    public readonly bool IsSwing;
    public readonly bool IsOverhead;
    public readonly bool IsPassiveUsage;
    public readonly bool HasMeleeWeapon;
    public readonly bool DefendItemIsShield;

    // Attacker's value of the wielded weapon's RelevantSkill / defender's value of the defend
    // item's RelevantSkill (0 when no weapon/skill).
    public readonly int AttackerWeaponSkill;
    public readonly int DefenderWeaponSkill;

    // Null when the agent/character is unavailable. Never default to 0 — race id 0 is a real race.
    public readonly int? AttackerRaceId;
    public readonly int? DefenderRaceId;

    // Monster.StringId of the attacker (null for none); service normalizes _settlement* suffixes.
    public readonly string AttackerMonsterId;
    public readonly bool IsAiControlled;

    // Roll captured at the boundary (MBRandom.RandomFloat) so the service stays deterministic.
    public readonly float RandomRoll;

    public CrushThroughContext(
        float totalAttackEnergy,
        bool isSwing,
        bool isOverhead,
        bool isPassiveUsage,
        bool hasMeleeWeapon,
        bool defendItemIsShield,
        int attackerWeaponSkill,
        int defenderWeaponSkill,
        int? attackerRaceId,
        int? defenderRaceId,
        string attackerMonsterId,
        bool isAiControlled,
        float randomRoll)
    {
        TotalAttackEnergy = totalAttackEnergy;
        IsSwing = isSwing;
        IsOverhead = isOverhead;
        IsPassiveUsage = isPassiveUsage;
        HasMeleeWeapon = hasMeleeWeapon;
        DefendItemIsShield = defendItemIsShield;
        AttackerWeaponSkill = attackerWeaponSkill;
        DefenderWeaponSkill = defenderWeaponSkill;
        AttackerRaceId = attackerRaceId;
        DefenderRaceId = defenderRaceId;
        AttackerMonsterId = attackerMonsterId;
        IsAiControlled = isAiControlled;
        RandomRoll = randomRoll;
    }
}
