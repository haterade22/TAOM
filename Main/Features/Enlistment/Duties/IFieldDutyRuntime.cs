using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Owns the lifecycle of ONE active field duty at a time (the record's <c>ActiveDutyId</c>
/// slot). <see cref="Start"/> detaches the player and spawns/resolves the target;
/// <see cref="HourlyUpdate"/> drives expiry/alt-completion/shift-timer; <see cref="OnTargetPartyDestroyed"/>
/// and <see cref="OnSettlementEntered"/> are completion triggers the orchestrator wires from
/// campaign events; <see cref="CancelActive"/> is the hygiene exit used by discharge and by
/// <see cref="HourlyUpdate"/> itself once the player is no longer enlisted.
/// </summary>
public interface IFieldDutyRuntime
{
    /// <summary>False (no state change) when a duty is already active, the spawn/settlement resolution fails, or the state transition is illegal.</summary>
    bool Start(DutyDefinition duty, double nowDays);

    void HourlyUpdate(double nowDays);

    /// <summary>No-op unless <paramref name="partyId"/> matches the active duty's target.</summary>
    void OnTargetPartyDestroyed(string partyId);

    /// <summary>No-op unless <paramref name="settlementId"/> matches the active duty's target settlement.</summary>
    void OnSettlementEntered(string settlementId, double nowDays);

    /// <summary>Destroys any spawned target and clears the active-duty fields. No reward, no penalty — used for discharge/config-drift cleanup.</summary>
    void CancelActive(string reason);
}
