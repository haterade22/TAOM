using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.MountDespawn.Hooks;

/// <summary>
/// Retires killed mounts a few seconds after they die. A mount is a full agent carrying a skeleton
/// and a live ragdoll, and a horse rig is heavier than a human one, so a cavalry battle accumulates
/// corpse agents that do nothing once the rider is gone.
///
/// This class owns the engine handles; every timing decision belongs to
/// <see cref="IDeadMountDespawnService"/>, which sees only indices and mission time.
/// </summary>
public sealed class MountDespawnMissionBehavior : MissionBehavior
{
    private const float SweepIntervalSeconds = 0.5f;

    private readonly IDeadMountDespawnService _service;
    private readonly IModLogger _logger;

    // index -> the killed mount. The service holds the schedule; this holds what to fade.
    private readonly Dictionary<int, Agent> _pending = new();

    // Sweep-local copy of the service's due list, so the fade loop never iterates a buffer another
    // object owns. Capped at MaxFadesPerSweep, so it stops reallocating after the first sweep.
    private readonly List<int> _dueScratch = new();

    private float _accumulator;
    private int _fadedThisMission;

    // BehaviorType=Other: this class inherits MissionBehavior (not MissionLogic) and does not
    // override MissionEnded/OnMissionResultReady, so it has no business in Mission.MissionLogics.
    // Returning Logic here caused vanilla AddMissionBehavior to do `MissionLogics.Add(this as MissionLogic)`
    // which evaluates to null and NREs the next CheckMissionEnded tick.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public MountDespawnMissionBehavior(IDeadMountDespawnService service, IModLogger logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

        // IsMount first: it is an inlined pointer read plus a bitwise AND, and it rejects almost
        // every death in the battle. IsEnabled reaches an MCM static through the provider, so it
        // is the more expensive of the two and belongs behind the selective one.
        if (affectedAgent == null || !affectedAgent.IsMount) return;
        if (!_service.IsEnabled) return;

        // Killed only. A mount removed as Routed fled the field and is still alive; Unconscious is a
        // human state. Every vanilla consumer that cares about a dead mount (MountAgentLogic's
        // lame-horse roll and harness return, CasualtyHandler, BattleAgentLogic) has already run by
        // the time this callback returns, so retiring the corpse later cannot affect loot or rewards.
        if (agentState != AgentState.Killed) return;

        var mission = Mission.Current;
        if (!MountDespawnMissionGate.IsEligible(mission)) return;

        _pending[affectedAgent.Index] = affectedAgent;
        _service.OnMountKilled(affectedAgent.Index, mission.CurrentTime);
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        if (affectedAgent == null) return;

        // The engine got there first (its own corpse pool, or our own fade completing). Dropping the
        // handle here is what makes index reuse safe: deletion always precedes an index being handed
        // to a new agent, and a deleted agent's property getters dereference native pointers that
        // Agent.Clear() has already zeroed.
        var index = affectedAgent.Index;
        _pending.Remove(index);
        _service.Forget(index);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        if (_pending.Count == 0) return;

        _accumulator += dt;
        if (_accumulator < SweepIntervalSeconds) return;
        _accumulator = 0f;

        var mission = Mission.Current;
        if (mission == null) return;

        // Both re-read live, never cached: MissionTeamAIType is assigned after OnBehaviorInitialize,
        // and the player can flip the MCM toggle mid-battle.
        if (!_service.IsEnabled) return;
        if (!MountDespawnMissionGate.IsEligible(mission)) return;

        // Copy before iterating. CollectDue hands back the service's own reused buffer, and FadeOut
        // can drive Mission.OnAgentDeleted synchronously; nothing on that path re-enters CollectDue
        // today, but that is an invariant spread across two files with nothing enforcing it. The
        // copy is at most 8 ints into a list that never reallocates after the first sweep, so the
        // insurance is free.
        _dueScratch.Clear();
        _dueScratch.AddRange(_service.CollectDue(mission.CurrentTime));
        for (var i = 0; i < _dueScratch.Count; i++)
            FadeOne(_dueScratch[i]);
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();

        if (_fadedThisMission > 0)
            _logger.LogInfo($"[MountDespawn] retired {_fadedThisMission} dead mount(s) this mission");

        _pending.Clear();
        _service.OnMissionEnd();
        _accumulator = 0f;
        _fadedThisMission = 0;
    }

    private void FadeOne(int index)
    {
        // CollectDue handed us a snapshot precisely so this loop is not enumerating a collection that
        // FadeOut can mutate: the fade may drive Mission.OnAgentDeleted synchronously, and that
        // callback removes from _pending.
        if (!_pending.TryGetValue(index, out var agent)) return;
        _pending.Remove(index);

        if (agent == null) return;

        try
        {
            if (agent.IsFadingOut()) return;

            // hideInstantly: false gives the dissolve, matching vanilla's own riderless-mount cleanup
            // in SpawningBehaviorBase. hideMount is irrelevant for a dead mount, which has no mount
            // of its own.
            agent.FadeOut(hideInstantly: false, hideMount: false);
            _fadedThisMission++;
        }
        catch (Exception ex)
        {
            // No vanilla call site fades an agent that is already Killed, so the native behavior is
            // not knowable from managed code. A throw here must not take the battle down with it.
            _logger.LogError($"[MountDespawn] FadeOut failed for agent {index}: {ex.Message}");
        }
    }
}
