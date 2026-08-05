namespace TAOM.Features.Enlistment.Duties;

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

    void OnSettlementEntered(string settlementId, double nowDays);

    void OnMobilePartyDestroyed(string partyId);

    /// <summary>Clears any active duty artifact without reward/penalty — call from the discharge consequence.</summary>
    void CancelActiveDuty(string reason);
}
