using System.Collections.Generic;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Constants sourced from the clean-room spec (the combat-mechanics spec).
// Deserialized with ObjectCreationHandling.Replace so a JSON list/dict REPLACES the compiled
// default instead of Json.NET's append-merge (which would duplicate list entries).
public class CombatMechanicsConfig
{
    public bool Enabled { get; set; } = true;
    public CrushThroughConfig CrushThrough { get; set; } = new CrushThroughConfig();
    public ChargeKnockdownConfig ChargeKnockdown { get; set; } = new ChargeKnockdownConfig();
    public CreatureCombatConfig Creatures { get; set; } = new CreatureCombatConfig();
    public ShieldPenetrationConfig ShieldPenetration { get; set; } = new ShieldPenetrationConfig();

    // Keyed by race NAME (precedent: raceage/race_age_config.json). Keys are validated against
    // IRaceManager.IsValidRaceName lazily (RaceCombatModifiersResolver) because the race registry
    // is engine state unavailable at provider construction.
    public Dictionary<string, RaceCombatModifiers> RaceModifiers { get; set; } = new Dictionary<string, RaceCombatModifiers>
    {
        ["dwarf"] = new RaceCombatModifiers { CtbDefenseBonus = 15f, KnockdownResistanceMultiplier = 2.5f, StaggerThresholdMultiplier = 1.5f },
        ["elf"] = new RaceCombatModifiers { CtbAttackBonus = 20f, RemoveNonOverheadPenalty = true },
        // Sauron (lord_1_17) was race "elf" until #321 split him into his own race; the CTB/overhead
        // baseline mirrors elf so the split dropped nothing. On top: charge-knockdown resistance above
        // the dwarf ceiling (2.5) and the strongest swing-energy bias (orc 0.15) — the 1.40-scale
        // Dark Lord keeps elf Monster weight 80, so the resistance row is what stops horse-bowling.
        ["sauron"] = new RaceCombatModifiers { CtbAttackBonus = 20f, RemoveNonOverheadPenalty = true, KnockdownResistanceMultiplier = 3.0f, SwingEnergyBonusFactor = 0.20f },
        ["orc"] = new RaceCombatModifiers { SwingEnergyBonusFactor = 0.15f },
        ["uruk_hai"] = new RaceCombatModifiers { SwingEnergyBonusFactor = 0.10f, KnockdownResistanceMultiplier = 1.25f },
    };
}

public class CrushThroughConfig
{
    public bool SkillBasedEnabled { get; set; } = true;

    // Extra-CTB path only; the engine's base 58f overhead path is untouched (falls through to base).
    public float ExtraCrushThroughEnergyThreshold { get; set; } = 25f;
    public int SkillDeadZone { get; set; } = 30;
    public int SkillTargetDelta { get; set; } = 200;
    public float MaxSkillChance { get; set; } = 0.5f;
    public float SkillCurveExponent { get; set; } = 0.008097f;
    public float EnergyRampMarginFactor { get; set; } = 0.27f;
    public float NonOverheadPenaltyFactor { get; set; } = 0.5f;

    public bool MonsterAutoCrushEnabled { get; set; } = true;
    public List<string> MonsterCrushMonsterIds { get; set; } = new List<string>
    {
        "cave_troll", "hill_troll", "taom_war_elephant", "taom_mumakil", "spider", "sauron",
    };

    public bool OrcShieldCrushEnabled { get; set; } = true;
    public List<string> OrcShieldCrushRaces { get; set; } = new List<string>
    {
        "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk", "sauron",
    };
}

public class ChargeKnockdownConfig
{
    public bool Enabled { get; set; } = true;
    public bool IncludeRiderWeight { get; set; } = true;

    // 6.0 = Native (horse 400 + rider 80) / human 80 — keeps an unmodified horse-vs-man charge ≈ vanilla.
    public float NeutralWeightRatio { get; set; } = 6f;
    public float AutoKnockdownWeightRatio { get; set; } = 8f;
    public float AutoKnockdownMinSpeedFactor { get; set; } = 0.4f;

    // Used when the charger's Monster has no relative_speed_limit_for_charge (engine default float.MaxValue).
    // 4.3 = Native horse.
    public float DefaultChargeSpeedReference { get; set; } = 4.3f;
    public float MinPenetrationFactor { get; set; } = 0.25f;
    public float MaxPenetrationFactor { get; set; } = 2.5f;

    // Vanilla SandBox value; also exposed via the GetHorseChargePenetration() override so the
    // engine fall-through path and TAOM's Branch B use the same number.
    public float HorseChargePenetration { get; set; } = 0.4f;
}

public class CreatureCombatConfig
{
    public bool CleaveEnabled { get; set; } = true;
    public List<string> CleaveMonsterIds { get; set; } = new List<string>
    {
        "cave_troll", "hill_troll", "taom_mumakil", "sauron",
    };
    public float CleaveRemainingMomentumFactor { get; set; } = 0.3f;

    public bool UnstoppableEnabled { get; set; } = true;

    // Damage at or below the threshold is shrugged off (no stagger/knockback/knockdown/dismount).
    public Dictionary<string, int> UnstoppableDamageThresholds { get; set; } = new Dictionary<string, int>
    {
        ["cave_troll"] = 15,
        ["hill_troll"] = 15,
        ["taom_war_elephant"] = 25,
        ["taom_mumakil"] = 30,
        ["spider"] = 10,
    };
}

// Ships DISABLED (2026-08-17). The mechanic is intact and re-enableable per item id or class, but
// the default is vanilla parity: javelins pierce shields only via the engine's own Throwing.Impale
// grant in SandboxAgentApplyDamageModel.DecideMissileWeaponFlags.
//
// These compiled values are the revert-to target when the JSON fails validation, so they must match
// the shipped JSON. A `true` here would resurrect the mechanic from a single malformed edit.
public class ShieldPenetrationConfig
{
    public bool Enabled { get; set; } = false;

    // WeaponClass enum member names (validated via Enum.TryParse at load; unknown → skip + warn).
    public List<string> WeaponClasses { get; set; } = new List<string>();
    public List<string> ItemIds { get; set; } = new List<string>();
    public bool AddMultiplePenetration { get; set; } = false;

    // Was a workaround for a native shield-damage underestimation when penetration flags are added
    // at RUNTIME only (TW forums 470085/470117, filed ~1.2.x). Verified against 1.4.8 and the
    // premise does not hold: MissionCombatMechanicsHelper.ComputeBlowDamageOnShield selects the
    // missile shield multiplier by weapon CLASS first (ThrowingAxe → 0.3f, Javelin → 0.5f) and only
    // consults CanPenetrateShield/MultiplePenetration for classes that match neither. So for
    // Javelin — the only class this was ever pointed at — there is no flag-dependent
    // underestimation, and dividing by 0.3 was a straight 3.33x on shield damage. That number also
    // feeds the penetration gate in Mission.HandleMissileCollisionReaction
    // (damage > ShieldPenetrationOffset + ShieldPenetrationFactor * shieldArmor), so it made
    // javelins pass through shields that should have blocked them.
    //
    // The divisor is kept in the schema for a future weapon class whose flags DO drive the native
    // branch; it is inert while the toggle is false.
    public bool RuntimeShieldDamageCorrectionEnabled { get; set; } = false;
    public float RuntimeShieldDamageCorrectionDivisor { get; set; } = 0.3f;
}
