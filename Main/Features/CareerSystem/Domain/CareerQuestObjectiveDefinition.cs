namespace TAOM.Features.CareerSystem.Domain;

/// <summary>
/// The kind of progress an objective tracks. Each value maps to exactly one verified 1.4.5
/// <c>CampaignEvents</c> subscription in the quest shell + an evaluation branch in
/// <c>CareerQuestService</c>. Keep this in sync with both, and add one (input, branch) test
/// per value (see feedback_per_branch_dispatch_test_enumeration).
/// </summary>
public enum CareerQuestObjectiveType
{
    /// <summary>Win N field/siege battles (player is a participant on the winning side).</summary>
    WinBattles,

    /// <summary>Raise the skill named in <see cref="CareerQuestObjectiveDefinition.Param"/> to ≥ Target.</summary>
    SkillThreshold,

    /// <summary>Capture N settlements (player clan becomes the new owner).</summary>
    SettlementsCaptured,

    /// <summary>Win N tournaments.</summary>
    TournamentsWon,

    /// <summary>Reach ≥ Target clan renown.</summary>
    RenownThreshold,

    /// <summary>Accumulate ≥ Target gold (current hero gold reaches the threshold).</summary>
    GoldAccumulated,

    /// <summary>Defeat N enemy lords in battle — fires when the player's party takes a lord of an at-war faction PRISONER (capturing, not executing).</summary>
    DefeatEnemyLords,

    /// <summary>Enter a settlement of the type named in <see cref="CareerQuestObjectiveDefinition.Param"/> (e.g. "Town", "Castle") N times. Counts each entry (not distinct settlements).</summary>
    VisitSettlementType
}

/// <summary>
/// One trackable objective within a career quest. Immutable; authored in
/// <c>taom_career_quests.xml</c> and validated by <c>CareerQuestConfigProvider</c> before use.
/// </summary>
public sealed class CareerQuestObjectiveDefinition
{
    public CareerQuestObjectiveType Type { get; }

    /// <summary>Target count / threshold. Must be &gt; 0 (validated at load).</summary>
    public int Target { get; }

    /// <summary>Optional secondary key (skill id for SkillThreshold, settlement type for VisitSettlementType). Empty otherwise.</summary>
    public string Param { get; }

    /// <summary>Localized journal task label, e.g. "{=key}Slay the enemies of Gondor". Shown next to the progress bar.</summary>
    public string TaskKey { get; }

    public CareerQuestObjectiveDefinition(CareerQuestObjectiveType type, int target, string param, string taskKey)
    {
        Type = type;
        Target = target;
        Param = param ?? "";
        TaskKey = taskKey ?? "";
    }
}
