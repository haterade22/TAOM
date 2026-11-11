using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// Hands out the roles the muster planned once the armies stand: the captain leads his unit's formation, the banner
/// bearer gets the culture's banner through the engine's BannerBearerLogic, the horn blower is marked for
/// <see cref="CustomBattleHornCallBehavior"/>. Then one census line per side. Added only to a mustered Custom Battle
/// (<see cref="CustomBattleMuster.AddMissionBehaviors"/>). Boundary code (raw Mission and Agent); game-tested per ADR-008.
/// </summary>
internal sealed class CustomBattleRolesBehavior : MissionBehavior
{
    private const float SpawnSettleSeconds = 1.5f;

    private readonly List<Agent> _hornBlowers = new List<Agent>();
    private bool _done;
    private float _readyAt = -1f;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    /// <summary>This battle's horn blowers; a handle is checked with AgentSlotIdentity before use.</summary>
    internal IReadOnlyList<Agent> HornBlowers => _hornBlowers;

    public override void OnMissionTick(float dt)
    {
        if (_done) return;
        var mission = Mission;
        if (mission == null || mission.Agents.Count == 0) return;
        // agents arrive over a few frames: let the spawn finish
        if (_readyAt < 0f) { _readyAt = mission.CurrentTime + SpawnSettleSeconds; return; }
        if (mission.CurrentTime < _readyAt) return;
        _done = true;
        try { CustomBattleRoleAssigner.Hand(mission, CustomBattleMuster.Plan, _hornBlowers); }
        catch (Exception e) { CustomBattleMuster.Log("roles: " + e); }
        try { CustomBattleRoleAssigner.Census(mission); }
        catch (Exception e) { CustomBattleMuster.Log("census: " + e.Message); }
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _hornBlowers.Clear();
    }
}
