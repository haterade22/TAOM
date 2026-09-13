using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// State machine driver for the SmartCavalryAI feature. Owns one
/// <c>CavalryFormationState</c> per cavalry formation across the lifetime of a mission.
/// Tick on every mission frame for every cavalry formation; cleared on
/// <see cref="OnMissionEnd"/>.
/// </summary>
public interface ICavalryChargeService
{
    /// <summary>Patch31 postfix entry point: the player has just ordered a cavalry formation
    /// to Charge or ChargeToTarget. From Idle this decides reroute vs line-charge and starts the
    /// state machine. While a cycle is already running it means "charge now": the formation
    /// jumps to Charging at <paramref name="targetToken"/> without waiting for a line.
    /// <paramref name="targetToken"/> is an opaque handle the command adapter can later use to
    /// re-issue ChargeToTarget against the same formation.</summary>
    void HandleChargeOrder(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float currentMissionTime);

    /// <summary>Patch31b postfix entry point: the player's targeted charge arrives as a plain Charge
    /// (already handled, at the nearest enemy) followed by <c>Formation.SetTargetFormation(target)</c>.
    /// Re-points a running cycle at the formation the player actually chose: a Forming line is
    /// redrawn toward it, a Charging formation re-issues its charge at it, any other state keeps the
    /// token for its next step. No-op when the formation is not mid-cycle.</summary>
    void RetargetCycle(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float currentMissionTime);

    /// <summary>MissionBehavior tick for one formation. Called for every live formation on the
    /// player team, cavalry or not, so a cycle whose riders dismounted still reaches its exit.</summary>
    void Tick(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        float dt,
        float currentMissionTime);

    /// <summary>Another order took the formation (the player pressed anything but F3, or the
    /// team AI now commands it): drop the cycle for that formation without issuing any order,
    /// so the newer order stands. No-op when the formation is not mid-cycle.</summary>
    void CancelCharge(object formationKey);

    /// <summary>True while any formation is mid-cycle. The mission behavior keeps ticking while
    /// this is true even with the feature toggled off, so a cycle the toggle interrupted can be
    /// handed back to a vanilla charge instead of leaving riders on a Move nobody will lift.</summary>
    bool HasActiveCycles { get; }

    /// <summary>Clears all per-formation state. Must be called from
    /// <c>MissionBehavior.OnEndMission</c>; without this, state leaks across missions in
    /// the IoC singleton.</summary>
    void OnMissionEnd();

    /// <summary>Test/debug accessor. Returns <c>Idle</c> when no entry exists for the
    /// given formation key.</summary>
    CavalryState GetState(object formationKey);
}
