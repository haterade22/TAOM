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
    public SitOutConfig SitOut { get; set; } = new SitOutConfig();
}

/// <summary>
/// The 'Leave it to the others' rule (#830). Shipped values are in the <c>sitOut</c> section of
/// enlistment_config.json and must equal these defaults (pinned by the config provider tests).
/// </summary>
public sealed class SitOutConfig
{
    /// <summary>Player side strength share at or above which a healthy soldier may stay back. In (0.5, 1].</summary>
    public float OverwhelmingShare { get; set; } = 0.85f;

    /// <summary>Trust docked when a healthy soldier uses that allowance. At least 0.</summary>
    public int TrustCost { get; set; } = 1;

    /// <summary>Multiplier on the base win/loss XP of a battle he sat out. In [0, 1].</summary>
    public float BaseXpFactor { get; set; } = 0.5f;
}

/// <summary>Aggregate for enlistment_duties.json (the duty/incident content tables).</summary>
public sealed class EnlistmentDutiesConfig
{
    public List<DutyDefinition> FieldDuties { get; set; } = new List<DutyDefinition>();
    public List<InteractiveDutyDefinition> InteractiveDuties { get; set; } = new List<InteractiveDutyDefinition>();
    public List<IncidentDefinition> Incidents { get; set; } = new List<IncidentDefinition>();
}

/// <summary>
/// World snapshot for the duty scheduler.
/// Pure data; the snapshot service owns the engine reads.
/// </summary>
public sealed class ArmyRhythmSnapshot
{
    public bool InArmy { get; set; }
    public bool SiegePressure { get; set; }
    public bool Naval { get; set; }
    public bool Blockade { get; set; }
    public bool QuietGarrison { get; set; }
    public bool Marching { get; set; }
    public bool PreBattle { get; set; }
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
    public int Trust { get; set; }
    public int DeferredWages { get; set; }
}
