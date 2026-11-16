namespace TAOM.Features.CombatMechanics.Domain;

// Boundary DTO for the Cleaving Blows decision (ADR-007). Built by CombatMechanicsHooks from the sealed engine params; the
// service never sees Agent, Blow or MissionWeapon. Every field is a primitive read at the boundary, so the service stays
// deterministic and testable without the game.
public readonly struct CleavingBlowContext
{
    // The attacker's own object, used only as the key of the per-swing chain counter (never an index: a deleted agent's slot is
    // handed to the next one).
    public readonly object? AttackerIdentity;

    public readonly CleaveAttackerKind AttackerKind;
    public readonly bool AttackerIsHuman;
    public readonly bool AttackerIsMounted;

    // The blow's own strike type: a swing, not a thrust.
    public readonly bool IsSwing;

    // A bash or kick.
    public readonly bool IsAlternativeAttack;

    // A couched or braced attack.
    public readonly bool IsPassiveAttack;
    public readonly bool IsHorseCharge;
    public readonly bool IsColliderAgent;

    // The hit struck a shield, or the back of a carried one.
    public readonly bool BlockedByShield;

    // The weapon's other bone hit, not the one that cuts.
    public readonly bool HitWithAnotherBone;

    // The blow carries the engine's ShrugOff flag (troll stagger immunity and a live Stand Fast set it).
    public readonly bool VictimShruggedOff;

    // The victim's health is spent after this blow.
    public readonly bool IsFatal;

    public readonly int InflictedDamage;
    public readonly float AbsorbedByArmor;

    // WeaponClass.ToString() of the weapon's current usage, or null with no weapon.
    public readonly string? WeaponClassName;

    // The raw skill each side wields: the attacker's for his weapon, the victim's for his wielded item (0 with none).
    public readonly int AttackerWeaponSkill;
    public readonly int VictimWeaponSkill;

    public CleavingBlowContext(
        object? attackerIdentity,
        CleaveAttackerKind attackerKind,
        bool attackerIsHuman,
        bool attackerIsMounted,
        bool isSwing,
        bool isAlternativeAttack,
        bool isPassiveAttack,
        bool isHorseCharge,
        bool isColliderAgent,
        bool blockedByShield,
        bool hitWithAnotherBone,
        bool victimShruggedOff,
        bool isFatal,
        int inflictedDamage,
        float absorbedByArmor,
        string? weaponClassName,
        int attackerWeaponSkill,
        int victimWeaponSkill)
    {
        AttackerIdentity = attackerIdentity;
        AttackerKind = attackerKind;
        AttackerIsHuman = attackerIsHuman;
        AttackerIsMounted = attackerIsMounted;
        IsSwing = isSwing;
        IsAlternativeAttack = isAlternativeAttack;
        IsPassiveAttack = isPassiveAttack;
        IsHorseCharge = isHorseCharge;
        IsColliderAgent = isColliderAgent;
        BlockedByShield = blockedByShield;
        HitWithAnotherBone = hitWithAnotherBone;
        VictimShruggedOff = victimShruggedOff;
        IsFatal = isFatal;
        InflictedDamage = inflictedDamage;
        AbsorbedByArmor = absorbedByArmor;
        WeaponClassName = weaponClassName;
        AttackerWeaponSkill = attackerWeaponSkill;
        VictimWeaponSkill = victimWeaponSkill;
    }
}
