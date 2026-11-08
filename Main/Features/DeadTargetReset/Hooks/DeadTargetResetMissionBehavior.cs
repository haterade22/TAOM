using System;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Features.DeadTargetReset.Hooks;

/// <summary>
/// Resets every AI soldier's target that is dead or unconscious, once a second, in every mission (#1096). The engine's corpse
/// handler fades a dead agent without a rout and deletes it 30 s later; the delete sets each soldier's target on it to -1 but
/// leaves the "target is valid" flag, and the next AI tick reads agent slot -1 (TaleWorlds.Native 0x6B716E). The reset is the
/// engine's own: <c>Agent.InvalidateTargetAgent</c> reaches the same native reset a rout runs, which clears the target and the
/// flag together. Never <c>SetTargetAgent(null)</c>: natively that leaves the flag set.
///
/// It runs in <c>OnMissionTick</c>, on the main thread, before the engine starts the asynchronous agent tick. Only active,
/// AI-controlled agents are touched, so a co-op puppet (Controller None) is never reset. A fresh instance is built for every
/// mission.
/// </summary>
public sealed class DeadTargetResetMissionBehavior : MissionBehavior
{
    private readonly IModLogger _logger;
    private readonly DeadTargetSweep _sweep = new();
    private bool _disabled;

    public DeadTargetResetMissionBehavior(IModLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Other, not Logic: this class derives from MissionBehavior, and Logic makes vanilla add a null to MissionLogics.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled || !_sweep.Due(dt)) return;

        try
        {
            MissionThreadGuard.NoteCall("DeadTargetResetMissionBehavior.Sweep", _logger.LogWarning);
            var reset = 0;
            foreach (var agent in Mission.Agents)
            {
                if (!agent.IsActive() || !agent.IsAIControlled) continue;
                var target = agent.GetTargetAgent();
                if (target == null || !DeadTargetSweep.IsStale(target.State)) continue;
                agent.InvalidateTargetAgent();
                reset++;
            }

            _sweep.Record(reset);
        }
        catch (Exception ex)
        {
            // One failure stops the sweep for this mission: a fault that repeats every second would flood the log.
            _disabled = true;
            _logger.LogError($"[DeadTarget] sweep failed, off for this mission: {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        _logger.LogInfo(_sweep.Summary());
    }
}
