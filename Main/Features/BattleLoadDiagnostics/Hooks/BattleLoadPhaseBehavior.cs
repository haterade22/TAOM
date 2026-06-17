using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 6 — first OnMissionTick "battle playable" marker, then closes the loading window
// so the stall watchdog stands down and phase-5 stops logging reinforcement spawns.
// Inherits MissionLogic (NOT just MissionBehavior) per
// feedback_missionbehaviortype_logic_requires_missionlogic_inheritance — a BehaviorType=Logic
// behavior that isn't a MissionLogic null-casts and NREs every tick in CheckMissionEnded.
public sealed class BattleLoadPhaseBehavior : MissionLogic
{
    private readonly IBattleLoadDiagnosticsService _service;
    private readonly IBattleLoadStallMarker _stallMarker;
    private bool _playableLogged;

    public BattleLoadPhaseBehavior(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
    {
        _service = service;
        _stallMarker = stallMarker;
    }

    public override void OnMissionTick(float dt)
    {
        if (_playableLogged) return;
        _playableLogged = true;
        try
        {
            var mission = Mission;
            _service.LogBattlePlayable(mission?.SceneName ?? "<null>", mission?.Agents?.Count ?? 0);
        }
        catch { /* diagnostic only */ }
        finally
        {
            // Reaching a tick means the load finished — stop watching + clear the inflight
            // marker so no stall notice fires next session.
            BattleLoadLoadingWindow.Close();
            try { _stallMarker?.ClearInflight(); } catch { /* best-effort */ }
        }
    }

    public override void OnEndMissionInternal()
    {
        _playableLogged = false;
        BattleLoadLoadingWindow.Close();
        try { _stallMarker?.ClearInflight(); } catch { /* best-effort */ }
    }
}
