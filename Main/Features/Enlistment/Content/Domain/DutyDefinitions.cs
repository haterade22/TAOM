using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>Reward payload for duty/incident outcomes. All amounts >= 0 (provider-enforced);
/// failure paths pay zero gold by design (the donor paid gold even on failure).</summary>
public sealed class RewardSpec
{
    public int ServiceXp { get; set; }
    public int Gold { get; set; }
    public string SkillId { get; set; } = "";
    public int SkillXp { get; set; }
    public int Trust { get; set; }
    public int Relation { get; set; }
    public ReputationDomain RepDomain { get; set; } = ReputationDomain.None;
    public int RepAmount { get; set; }
}

/// <summary>Eligibility gates shared by field duties, interactive duties, and incidents.</summary>
public sealed class GateSpec
{
    public ServiceRank MinRank { get; set; } = ServiceRank.Recruit;
    public int MinTrust { get; set; } = -10;
    public int MaxTrust { get; set; } = 20;

    /// <summary>Contexts that must be active (any-of). Empty = no requirement. Known set: siege, naval, blockade, army, garrison, march.</summary>
    public List<string> RequiredContexts { get; set; } = new List<string>();
    public List<string> ExcludedContexts { get; set; } = new List<string>();

    /// <summary>Assignments this duty prefers (selection weight, not a hard gate). Empty = all.</summary>
    public List<ServiceAssignment> AssignmentAffinity { get; set; } = new List<ServiceAssignment>();
}

/// <summary>One field duty (detached map mission). 13 data rows over 5 mechanics.</summary>
public sealed class DutyDefinition
{
    public string Id { get; set; } = "";
    public DutyMechanic Mechanic { get; set; }
    public DutyTargetKind TargetKind { get; set; }
    public DutyTargetAi TargetAi { get; set; } = DutyTargetAi.PatrolAnchor;
    public int DeadlineDays { get; set; } = 4;
    public int TrustDeadlineBonusDays { get; set; } = 1;
    public int TrustBonusThreshold { get; set; } = 10;

    /// <summary>Optional alternate completion trigger. Known set: "", "EnemyContact".</summary>
    public string AltCompletion { get; set; } = "";

    public RewardSpec ReportReward { get; set; } = new RewardSpec();
    public GateSpec Gates { get; set; } = new GateSpec();

    /// <summary>Companion-support skills that boost this duty (replaces the donor's per-duty switch).</summary>
    public List<string> SupportSkills { get; set; } = new List<string>();
}

/// <summary>One choice on an interactive duty popup.</summary>
public sealed class DutyOptionSpec
{
    public string Key { get; set; } = "";

    /// <summary>Primary skill for the check; secondary makes it best-of-two (donor's TrainRecruits shape).</summary>
    public string SkillId { get; set; } = "";
    public string SecondarySkillId { get; set; } = "";
    public int Difficulty { get; set; } = 60;
    public bool RankBonusApplies { get; set; }
    public RewardSpec SuccessReward { get; set; } = new RewardSpec();
    public RewardSpec FailureReward { get; set; } = new RewardSpec();
}

/// <summary>One interactive (in-place popup) duty. 11 data rows through one presenter.</summary>
public sealed class InteractiveDutyDefinition
{
    public string Id { get; set; } = "";
    public GateSpec Gates { get; set; } = new GateSpec();
    public DutyOptionSpec OptionA { get; set; } = new DutyOptionSpec();
    public DutyOptionSpec OptionB { get; set; } = new DutyOptionSpec();
}

/// <summary>Camp-tension incident (no accept/decline gate — it happens TO you). 3 data rows.</summary>
public sealed class IncidentDefinition
{
    public string Id { get; set; } = "";
    public float Chance { get; set; } = 0.15f;
    public GateSpec Gates { get; set; } = new GateSpec();
    public DutyOptionSpec OptionA { get; set; } = new DutyOptionSpec();
    public DutyOptionSpec OptionB { get; set; } = new DutyOptionSpec();

    /// <summary>Known set: "", "ReleaseDeferredPay" (pay-delay incident frees part of the arrears).</summary>
    public string Effect { get; set; } = "";
}
