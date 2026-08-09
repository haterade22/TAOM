namespace TAOM.Features.Enlistment.Duties;

/// <summary>Why an explicit "any work for me?" request did or did not produce a duty.</summary>
public enum DutyRequestResult
{
    NotEnlisted = 0,
    AlreadyOnDuty = 1,

    /// <summary>The rotation has nothing right now. A normal answer, not a failure.</summary>
    NoWorkAvailable = 2,

    DutyAssigned = 3,
}

/// <summary>
/// Thin router the campaign behavior wires into: hourly/daily ticks, the two completion
/// triggers (settlement entered, target party destroyed), and the discharge hygiene call.
/// Owns NO logic itself beyond enlisted/active-duty gating — <see cref="IDutyRotationPolicy"/>,
/// <see cref="IDutySelector"/>, <see cref="IFieldDutyRuntime"/>, and
/// <see cref="IInteractiveDutyPresenter"/> do the actual work.
/// </summary>
public interface IDutyOrchestrationService
{
    /// <summary>Drives the active field duty's expiry/completion checks. No-op when not enlisted.</summary>
    void HourlyTick(double nowDays);

    /// <summary>Rolls an incident, else a duty offer, when no duty is active. No-op when not enlisted.</summary>
    void DailyOfferTick(double nowDays, double hourOfDay);

    /// <summary>
    /// Ask the commander for work now. Shares ONE offer path with <see cref="DailyOfferTick"/> and
    /// the same rotation cadence — asking cannot conjure work the rotation would not have given.
    /// </summary>
    DutyRequestResult RequestDutyNow(double nowDays, double hourOfDay);

    /// <summary>Clears any active duty artifact without reward/penalty — call from the discharge consequence.</summary>
    void CancelActiveDuty(string reason);
}
