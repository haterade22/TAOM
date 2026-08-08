using System.Collections.Generic;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Picks WHICH duty/incident gets offered, given that <see cref="IDutyRotationPolicy"/>
/// already decided an offer should happen today. Filters by <see cref="DutyGateEvaluator"/>
/// gates, weights by assignment affinity multiplied by the row's own <c>GateSpec.Weight</c>
/// rarity (absent = 1; outside [1,1000] the row is skipped), and applies field/interactive anti-repeat via
/// <c>RecentDutyIds</c> (not applied to incidents — they have their own cooldown in
/// <see cref="IDutyRotationPolicy.ShouldRollIncident"/>). Deterministic given the same
/// <c>IRandomProvider</c> sequence.
/// </summary>
public interface IDutySelector
{
    /// <summary>
    /// Combines the eligible field-duty and interactive-duty pools into one weighted pick.
    /// <paramref name="recentDutyIds"/> entries are excluded unless <paramref name="pressure"/>
    /// is true (the donor's "anti-repeat lifts under pressure" rule, simplified: any row in
    /// the list is skipped rather than modeling a day-count expiry — the caller caps the
    /// list at 5 entries).
    /// </summary>
    DutyOfferSelection SelectOffer(
        EnlistmentDutiesConfig duties,
        ServiceProgressSnapshot progress,
        ArmyRhythmSnapshot rhythm,
        IReadOnlyList<string> recentDutyIds,
        bool pressure);

    /// <summary>
    /// Rolls each eligible incident's own <c>Chance</c> in data order and returns the first
    /// that passes, or null when none do (or none are eligible).
    /// </summary>
    IncidentDefinition SelectIncident(
        IReadOnlyList<IncidentDefinition> incidents,
        ServiceProgressSnapshot progress,
        ArmyRhythmSnapshot rhythm);
}
