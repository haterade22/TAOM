using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>Aggregate for enlistment_config.json (designer tunables — thresholds, tables, weights).</summary>
public sealed class EnlistmentContentConfig
{
    public ProgressionTables Progression { get; set; } = new ProgressionTables();
    public WagePolicyConfig WagePolicy { get; set; } = new WagePolicyConfig();
    public MeritScoringConfig MeritScoring { get; set; } = new MeritScoringConfig();
    public List<MeritBand> MeritBands { get; set; } = new List<MeritBand>();
    public SchedulerConfig Scheduler { get; set; } = new SchedulerConfig();
    public List<PromotionRequirement> Promotions { get; set; } = new List<PromotionRequirement>();
}

/// <summary>Aggregate for enlistment_duties.json (the duty/incident content tables).</summary>
public sealed class EnlistmentDutiesConfig
{
    public List<DutyDefinition> FieldDuties { get; set; } = new List<DutyDefinition>();
    public List<InteractiveDutyDefinition> InteractiveDuties { get; set; } = new List<InteractiveDutyDefinition>();
    public List<IncidentDefinition> Incidents { get; set; } = new List<IncidentDefinition>();
}

/// <summary>
/// Hourly-cached world snapshot for the duty scheduler (replaces the donor's
/// GetCurrentArmyRhythmState — 2 full MobileParty.All scans × 11 call sites per tick chain).
/// Pure data; the snapshot service owns the engine reads.
/// </summary>
public sealed class ArmyRhythmSnapshot
{
    public ServicePhase Phase { get; set; }
    public bool InArmy { get; set; }
    public bool SiegePressure { get; set; }
    public bool Naval { get; set; }
    public bool Blockade { get; set; }
    public bool LowSupplies { get; set; }
    public bool RecoveryState { get; set; }
    public bool QuietGarrison { get; set; }
    public bool Marching { get; set; }
    public bool PreBattle { get; set; }

    /// <summary>Faction-level war check (the donor's party-scan version was effectively always true).</summary>
    public bool ActiveCampaign { get; set; }

    public bool HighScrutiny { get; set; }
    public int WoundedCount { get; set; }
    public int RecruitCount { get; set; }
}

/// <summary>Progress counters the promotion evaluator + scheduler read (a view over the content store section).</summary>
public sealed class ServiceProgressSnapshot
{
    public ServiceRank Rank { get; set; }
    public ServiceAssignment Assignment { get; set; }
    public int DaysServed { get; set; }
    public int ServiceXp { get; set; }
    public int LeadershipSkill { get; set; }
    public int DutySuccesses { get; set; }
    public int DutyFailures { get; set; }
    public int Trust { get; set; }
    public int DeferredWages { get; set; }
}
