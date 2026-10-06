using System;
using TAOM.Core.Validation;

namespace TAOM.Features.CombatMechanics;

// Merges MCM live values (TaomSettings.Instance) over the validated JSON defaults
// (AlignmentDesertion pattern). Instance can be null very early in startup or if MCM fails to
// load — every read falls back to JSON. Every enabled-getter folds in the master toggle so
// master-off means all mechanics report disabled (= exactly the pre-feature behavior).
public sealed class CombatMechanicsSettingsProvider : ICombatMechanicsSettingsProvider
{
    private readonly CombatMechanicsConfig _defaults;

    // HOT PATH: read per melee blow (crush-through, cleave, stagger, shield penetration; the charge
    // knockdown settings only on a horse charge, the one blow CombatMechanicsHooks hands to
    // ChargeKnockdownService) and per mount stat update. Resolving TaomSettings.Instance walks MCM's
    // settings containers, so the reference is cached on its first non-null read and read THROUGH, never
    // snapshotted: MCM edits its one registered instance in place (reset and presets copy values into
    // it), so live MCM edits still apply. Lazy, not in the constructor, so a resolve before MCM is up
    // cannot pin the JSON fallbacks. Same contract as BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    internal CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;

    private bool MasterEnabled => Settings?.EnableCombatMechanics ?? _defaults.Enabled;

    public bool SkillCrushThroughEnabled => MasterEnabled && (Settings?.EnableSkillCrushThrough ?? _defaults.CrushThrough.SkillBasedEnabled);

    public bool MonsterCrushThroughEnabled => MasterEnabled && (Settings?.EnableMonsterCrushThrough ?? _defaults.CrushThrough.MonsterAutoCrushEnabled);

    public bool OrcShieldCrushEnabled => MasterEnabled && (Settings?.EnableOrcShieldCrush ?? _defaults.CrushThrough.OrcShieldCrushEnabled);

    public bool CreatureCleaveEnabled => MasterEnabled && (Settings?.EnableCreatureCleave ?? _defaults.Creatures.CleaveEnabled);

    public bool CreatureUnstoppableEnabled => MasterEnabled && (Settings?.EnableCreatureUnstoppable ?? _defaults.Creatures.UnstoppableEnabled);

    public bool ChargeKnockdownEnabled => MasterEnabled && (Settings?.EnableChargeKnockdown ?? _defaults.ChargeKnockdown.Enabled);

    public bool ShieldPenetrationEnabled => MasterEnabled && (Settings?.EnableShieldPenetration ?? _defaults.ShieldPenetration.Enabled);

    // No JSON sibling — the race table itself is the JSON side; this is a pure MCM kill switch.
    public bool RaceCombatModifiersEnabled => MasterEnabled && (Settings?.EnableRaceCombatModifiers ?? true);

    public bool CultureChargeDamageEnabled => MasterEnabled && (Settings?.EnableCultureChargeDamage ?? _defaults.ChargeDamage.Enabled);

    public float CrushThroughMaxChance
        => SettingClamp.Clamp(Settings?.CrushThroughMaxChance, _defaults.CrushThrough.MaxSkillChance, 0f, 1f);

    // MCM slider is an int [2,30]; without MCM the provider-validated JSON float applies as-is.
    // The slider floor is the validated NeutralWeightRatio: a below-neutral auto-knockdown ratio
    // would let Branch A auto-floor ordinary horse-vs-man charges — the exact state the JSON
    // ordering invariant (auto >= neutral) exists to prevent. Both entry points enforce one rule.
    public float ChargeAutoKnockdownWeightRatio
    {
        get
        {
            var mcm = Settings?.ChargeAutoKnockdownWeightRatio;
            if (!mcm.HasValue)
                return _defaults.ChargeKnockdown.AutoKnockdownWeightRatio;

            // The floor follows the LIVE neutral ratio (#610): both are sliders now, and the
            // invariant is between the two values the player set, not the JSON ones.
            return SettingClamp.Clamp(mcm.Value, 6, AutoKnockdownFloor(ChargeNeutralWeightRatio), 30);
        }
    }

    // #610: the three Branch B knobs. Slider bounds mirror TaomSettings; the JSON may go wider.
    public float ChargeNeutralWeightRatio
        => SettingClamp.Clamp(Settings?.ChargeNeutralWeightRatio, _defaults.ChargeKnockdown.NeutralWeightRatio, 1f, 30f);

    public float ChargeHorsePenetration
        => SettingClamp.Clamp(Settings?.ChargeHorsePenetration, _defaults.ChargeKnockdown.HorseChargePenetration, 0f, 1f);

    // Clamped to the JSON max so the min <= max invariant holds at both surfaces.
    public float ChargeMinPenetrationFactor
        => SettingClamp.Clamp(Settings?.ChargeMinPenetrationFactor, _defaults.ChargeKnockdown.MinPenetrationFactor, 0f, _defaults.ChargeKnockdown.MaxPenetrationFactor);

    /// <summary>The auto-knockdown slider's floor: the neutral ratio rounded up, never below 2.
    /// NaN reads as the minimum so a poisoned value cannot unlock Branch A on ordinary charges.</summary>
    public static int AutoKnockdownFloor(float neutralWeightRatio)
    {
        if (!FiniteFloatValidator.IsFinite(neutralWeightRatio))
            return 2;
        return Math.Max(2, (int)Math.Ceiling(neutralWeightRatio));
    }
}
