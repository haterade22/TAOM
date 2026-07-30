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

    /// <summary>
    /// Deliverable captives in the player's prison roster, clamped to <paramref name="cap"/>. Every
    /// non-hero prisoner counts regardless of troop type — TAOM's bandit-culture rank-and-file are
    /// ordinary faction troops, so a troop-type filter matches almost nothing (see the DeliverPersonnel
    /// regression tests).
    /// </summary>
    int CountDeliverableCaptives(IReadOnlyList<LotrCaptiveStack> stacks, int cap);

    /// <summary>
    /// How many to take from each stack to hand over <paramref name="count"/> captives — parallel to
    /// <paramref name="stacks"/>, hero stacks always 0. Shares its predicate with
    /// <see cref="CountDeliverableCaptives"/> so the turn-in gate and the handover cannot drift apart.
    /// </summary>
    IReadOnlyList<int> PlanCaptiveHandover(IReadOnlyList<LotrCaptiveStack> stacks, int count);

    /// <summary>Grant the definition's completion reward (gold + renown + optional item) to the hero.</summary>
    void ApplyRewards(LotrIssueDefinition def, float difficulty, ILotrIssueRewardAdapter hero);
}
