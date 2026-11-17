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

    /// <summary>Hammerhand (#895), the shield bash and kick knockdown; folds the master.</summary>
    bool ShieldBashKnockdownEnabled { get; }
    bool ShieldPenetrationEnabled { get; }

    /// <summary>Cleaving Blows; folds the master.</summary>
    bool CleavingBlowsEnabled { get; }

    /// <summary>Cleaving Blows for AI soldiers; needs <see cref="CleavingBlowsEnabled"/>, so it folds the master too.</summary>
    bool CleavingBlowsAiEnabled { get; }

    /// <summary>The most enemies one swing can strike, the first included; the MCM slider's 3 to 5 over the JSON value (3 is vanilla's best case for an axe, so the floor never reaches fewer men than vanilla).</summary>
    int CleavingBlowsMaxTargets { get; }
    bool RaceCombatModifiersEnabled { get; }

    /// <summary>Per-culture charge damage (#610); folds the master.</summary>
    bool CultureChargeDamageEnabled { get; }

    // MCM slider overrides of the matching JSON values (SafeClamp'd).
    float CrushThroughMaxChance { get; }
    float ChargeAutoKnockdownWeightRatio { get; }

    // #610: the three Branch B knobs, live. Read per hit by ChargeKnockdownService; the auto
    // ratio's floor derives from the LIVE neutral value so the two sliders keep auto >= neutral.
    float ChargeNeutralWeightRatio { get; }
    float ChargeHorsePenetration { get; }
    float ChargeMinPenetrationFactor { get; }
}
