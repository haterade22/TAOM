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
    /// <summary>Patch31 postfix entry point: a cavalry formation has just been ordered
    /// to Charge or ChargeToTarget. Decides reroute vs line-charge and starts the
    /// state machine. <paramref name="targetToken"/> is an opaque handle the command
    /// adapter can later use to re-issue ChargeToTarget against the same formation.</summary>
    void HandleChargeOrder(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        object targetToken,
        Vec3 targetPosition,
        float currentMissionTime);

    /// <summary>MissionBehavior tick for one cavalry formation.</summary>
    void Tick(
        IFormationAdapter cav,
        ICavalryCommandAdapter commands,
        IBattlefieldQueryAdapter battlefield,
        float dt,
        float currentMissionTime);

    /// <summary>Clears all per-formation state. Must be called from
    /// <c>MissionBehavior.OnEndMission</c> — without this, state leaks across missions in
    /// the IoC singleton.</summary>
    void OnMissionEnd();

    /// <summary>Test/debug accessor. Returns <c>Idle</c> when no entry exists for the
    /// given formation key.</summary>
    CavalryState GetState(object formationKey);
}
