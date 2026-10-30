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
    SpecialResourceUpgradeCostModifier,

    // Troop counters (#769 E10): what the leader's non-hero troops resist of the enemy's fear, knockdown
    // and crush-through. Class-filtered; read through CareerTroopHooks at the morale, stat and combat seams.
    TroopFearResistance,
    TroopKnockdownResistance,
    TroopCrushHold,

    // Campaign consumers (#769 E11): ten passives read by a GameModel override each. Sign convention: a positive
    // magnitude is "more" except where the name is a cost, a consumption or a penalty, which are authored
    // negative for a cut (RecruitCost, FoodConsumption, TradePrices), as TroopWages and TroopUpgradeCost are.
    // DailyGold and TroopTraining are counts; the rest are fractions.
    InfluenceGain,
    RecruitCost,
    PrisonerConversion,
    FoodConsumption,
    WoundRecovery,
    DailyGold,
    LootGold,
    RaidLoot,
    TroopTraining,
    TradePrices
}
