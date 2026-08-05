using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Cadence gate for the daily duty/incident offer roll: min days served, cooldown
/// (quiet vs. pressure), guaranteed-offer overdue check, and a trust/pressure-boosted
/// chance clamped to <c>[BaseOfferChance, MaxOfferChance]</c>. Does NOT pick which duty —
/// that's <see cref="IDutySelector"/>. Deterministic given the same <c>IRandomProvider</c> sequence.
/// </summary>
public interface IDutyRotationPolicy
{
    /// <summary>True when a new field/interactive duty offer should be attempted today. Caller guarantees no active duty is in progress.</summary>
    bool ShouldOfferDuty(SchedulerConfig scheduler, int daysServed, int trust, bool pressure, double nowDays, double? lastOfferDay);

    /// <summary>True when the incident-roll cadence (min days served + cooldown) permits an incident attempt today. The per-incident <c>Chance</c> field is applied separately by <see cref="IDutySelector.SelectIncident"/>.</summary>
    bool ShouldRollIncident(SchedulerConfig scheduler, int daysServed, double nowDays, double? lastIncidentDay);
}
