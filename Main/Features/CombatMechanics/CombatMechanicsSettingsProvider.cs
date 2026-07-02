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

    public CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    private bool MasterEnabled => TaomSettings.Instance?.EnableCombatMechanics ?? _defaults.Enabled;

    public bool SkillCrushThroughEnabled => MasterEnabled && (TaomSettings.Instance?.EnableSkillCrushThrough ?? _defaults.CrushThrough.SkillBasedEnabled);

    public bool MonsterCrushThroughEnabled => MasterEnabled && (TaomSettings.Instance?.EnableMonsterCrushThrough ?? _defaults.CrushThrough.MonsterAutoCrushEnabled);

    public bool OrcShieldCrushEnabled => MasterEnabled && (TaomSettings.Instance?.EnableOrcShieldCrush ?? _defaults.CrushThrough.OrcShieldCrushEnabled);

    public bool CreatureCleaveEnabled => MasterEnabled && (TaomSettings.Instance?.EnableCreatureCleave ?? _defaults.Creatures.CleaveEnabled);

    public bool CreatureUnstoppableEnabled => MasterEnabled && (TaomSettings.Instance?.EnableCreatureUnstoppable ?? _defaults.Creatures.UnstoppableEnabled);

    public bool ChargeKnockdownEnabled => MasterEnabled && (TaomSettings.Instance?.EnableChargeKnockdown ?? _defaults.ChargeKnockdown.Enabled);

    public bool ShieldPenetrationEnabled => MasterEnabled && (TaomSettings.Instance?.EnableShieldPenetration ?? _defaults.ShieldPenetration.Enabled);

    // No JSON sibling — the race table itself is the JSON side; this is a pure MCM kill switch.
    public bool RaceCombatModifiersEnabled => MasterEnabled && (TaomSettings.Instance?.EnableRaceCombatModifiers ?? true);

    public float CrushThroughMaxChance
        => SettingClamp.Clamp(TaomSettings.Instance?.CrushThroughMaxChance, _defaults.CrushThrough.MaxSkillChance, 0f, 1f);

    // MCM slider is an int [2,30]; without MCM the provider-validated JSON float applies as-is.
    // The slider floor is the validated NeutralWeightRatio: a below-neutral auto-knockdown ratio
    // would let Branch A auto-floor ordinary horse-vs-man charges — the exact state the JSON
    // ordering invariant (auto >= neutral) exists to prevent. Both entry points enforce one rule.
    public float ChargeAutoKnockdownWeightRatio
    {
        get
        {
            var mcm = TaomSettings.Instance?.ChargeAutoKnockdownWeightRatio;
            if (!mcm.HasValue)
                return _defaults.ChargeKnockdown.AutoKnockdownWeightRatio;

            var neutralFloor = Math.Max(2, (int)Math.Ceiling(_defaults.ChargeKnockdown.NeutralWeightRatio));
            return SettingClamp.Clamp(mcm.Value, 8, neutralFloor, 30);
        }
    }
}
