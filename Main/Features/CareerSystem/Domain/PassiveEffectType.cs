namespace TAOM.Features.CareerSystem.Domain;

// Every value here should have a runtime consumer registered in PassiveEffectConsumers —
// the load-time phantom gate warns on any career pip whose type nothing reads. `Special`
// is the parse-fallback sentinel; `BuffDuration` is reserved (unconsumed) and doubles as
// the phantom-gate test exemplar.
public enum PassiveEffectType
{
    Special,

    // Hero combat
    Health,
    Damage,
    Resistance,
    ArmorPenetration,
    SwingSpeed,
    MovementSpeed,
    Ammo,
    ShrugOff,
    StealthBonus,
    BuffDuration,

    // Mount
    MountHealth,
    MountChargeDamage,

    // Party
    PartyMovementSpeed,
    PartySpottingRange,
    PartySize,
    CompanionLimit,
    InventoryCapacity,

    // Troops
    TroopDamage,
    TroopResistance,
    TroopMorale,
    TroopWages,
    TroopUpgradeCost,
    TroopSurvival,

    // Hero recovery + economy
    HeroHealing,
    RenownGain,
    SmithingCostReduction,

    // Special resources (SpecialResources feature)
    SpecialResourceGain,
    SpecialResourceUpkeepModifier,
    SpecialResourceUpgradeCostModifier
}
