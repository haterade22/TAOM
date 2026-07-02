namespace TAOM.Features.CombatMechanics;

// MCM-over-JSON merge surface (AlignmentRecruitment pattern). Every "enabled" getter already
// folds in the master toggle — services check ONE property, and master-off means every mechanic
// reports disabled, so behavior is exactly the pre-feature career+vanilla model.
public interface ICombatMechanicsSettingsProvider
{
    bool SkillCrushThroughEnabled { get; }
    bool MonsterCrushThroughEnabled { get; }
    bool OrcShieldCrushEnabled { get; }
    bool CreatureCleaveEnabled { get; }
    bool CreatureUnstoppableEnabled { get; }
    bool ChargeKnockdownEnabled { get; }
    bool ShieldPenetrationEnabled { get; }
    bool RaceCombatModifiersEnabled { get; }

    // MCM slider overrides of the matching JSON values (SafeClamp'd).
    float CrushThroughMaxChance { get; }
    float ChargeAutoKnockdownWeightRatio { get; }
}
