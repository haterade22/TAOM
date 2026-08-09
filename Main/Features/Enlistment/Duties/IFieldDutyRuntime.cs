using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Owns the lifecycle of ONE active field duty at a time (the record's <c>ActiveDutyId</c> slot).
///
/// A field duty is CAMP WORK, not a journey. <see cref="Start"/> notes the duty and the hour it
/// finishes; <see cref="HourlyUpdate"/> resolves it with a single skill check once that hour
/// arrives. The player never leaves the column, is never made visible, and is never targetable.
///
/// That last sentence is the whole point of the design and is pinned by tests. The previous model
/// detached the player — <c>RestorePresence()</c> made their one-man, troop-less party visible and
/// active on the campaign map for days at a time. On 2026-08-08 a live session recorded exactly
/// what that costs: a duty started at 22:02:38, and 41 seconds later the player had been captured.
/// The duty then outlived the captivity and would have cost trust when it expired (#428).
/// </summary>
public interface IFieldDutyRuntime
{
    /// <summary>False (no state change) when the duty is null or one is already active.</summary>
    bool Start(DutyDefinition duty, double nowDays);

    /// <summary>Resolves the duty once its shift has elapsed. Also the hygiene exit for discharge and captivity.</summary>
    void HourlyUpdate(double nowDays);

    /// <summary>Clears the active-duty fields. No reward, no penalty — discharge, captivity, config drift.</summary>
    void CancelActive(string reason);
}
