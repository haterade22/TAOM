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

    /// <summary>Clan renown. Granted through GainRenownAction so the campaign's own renown-gained
    /// event fires — SAS writes Clan.Renown directly and skips every listener.</summary>
    public int Renown { get; set; }
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

    /// <summary>
    /// Relative rarity weight for the duty-offer pick, multiplied by the assignment-affinity
    /// weight. Absent from JSON = 1, which reproduces the pre-weighting uniform behaviour
    /// exactly, so every existing row is unaffected until it opts in.
    /// <para>
    /// A row whose weight falls outside [1, DutySelector.MaxSelectionWeight] is SKIPPED with a
    /// warning: in a cumulative-sum weighted pick a zero or negative weight is a selection bug,
    /// not a disable switch — it contributes nothing to the total yet still occupies a band
    /// boundary, so it can be returned by a roll that belongs to its neighbour. Delete the row
    /// or raise its gates to disable it.
    /// </para>
    /// <para>Incidents ignore this — they roll their own <c>Chance</c> instead of entering the weighted pool.</para>
    /// </summary>
    public int Weight { get; set; } = 1;
}

/// <summary>One field duty: camp work that occupies the player for a few hours and resolves
/// on a single skill check. 13 data rows. NOT a map mission — the player never detaches.</summary>
public sealed class DutyDefinition
{
    public string Id { get; set; } = "";

    /// <summary>Target the skill check must beat. Higher is harder; see ISkillCheckService.Passes.</summary>
    public int Difficulty { get; set; } = 60;

    /// <summary>How long the shift occupies the player, in HOURS. Deliberately not the old
    /// <c>deadlineDays</c>: that was a TRAVEL budget (service_shift allowed 1 day for a 4-hour
    /// shift, hideout_strike 6 days), so reusing it would re-create the multi-day wait this
    /// design removes.</summary>
    public int DurationHours { get; set; } = 6;

    /// <summary>Paid when the check fails. Same type as the success reward so both flow through
    /// the one Grant chokepoint; typically a little service XP and a small trust cost.</summary>
    public RewardSpec FailureReward { get; set; } = new RewardSpec();

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
