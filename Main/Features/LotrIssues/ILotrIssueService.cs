using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Pure decision logic for the LOTR issue system: eligibility selection, count/reward scaling,
/// objective completion, and reward application. No TaleWorlds types in any signature (ADR-007) so it
/// is fully unit-testable.
/// </summary>
public interface ILotrIssueService
{
    /// <summary>The issues the given hero may be offered (occupation + culture + relation gated).</summary>
    IReadOnlyList<LotrIssueDefinition> GetEligibleIssues(ILotrIssueGiverAdapter giver);

    /// <summary>Look up a definition by id (used by the quest shell to re-resolve its def after load).</summary>
    LotrIssueDefinition GetIssueById(string id);

    /// <summary>Base count plus difficulty-scaled count, rounded; difficulty is clamped to [0,1].</summary>
    int ComputeTargetCount(LotrIssueDefinition def, float difficulty);

    /// <summary>Base reward gold plus difficulty-scaled gold, rounded; difficulty is clamped to [0,1].</summary>
    int ComputeRewardGold(LotrIssueDefinition def, float difficulty);

    /// <summary>True once tracked progress meets the target.</summary>
    bool IsObjectiveSatisfied(int progress, int target);

    /// <summary>Grant the definition's completion reward (gold + renown + optional item) to the hero.</summary>
    void ApplyRewards(LotrIssueDefinition def, float difficulty, ILotrIssueRewardAdapter hero);
}
