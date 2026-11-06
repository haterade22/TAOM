using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The mission boundary of The Long Defeat (#1079): hands the engine's callbacks to the <see cref="LongDefeatRunHost"/>, which builds
/// the director on the first tick that is ready and tears the run down on both exits. MUST be <c>: MissionLogic</c>
/// (rca-looter-battle-nre-2026-05-24.md). <c>AfterStart</c> only marks the start as due, because an agent spawned inside the
/// engine's behavior loop lets a <c>SpawnAgent</c> hook add a behavior to the list being walked (rca-fords-of-war-2026-10-31.md).
/// It also answers team power for <c>TeamQuerySystem</c>.
/// </summary>
public sealed class LongDefeatMissionLogic : MissionLogic, IBattlePowerCalculationLogic
{
    // A constant, not a running total: it keeps RemainingPowerRatio and TotalPowerRatio near 1, so power never orders TacticBreachWalls' one-way retreat
    // (ShouldRetreat) or TacticDefendCastle's sally.
    private const float TeamPower = 1000000f;

    private readonly LongDefeatRunHost _host;
    private readonly Func<float> _missionTime;

    internal LongDefeatMissionLogic(RunPlan plan, LongDefeatSession session, IModLogger logger, IOutcomeLog outcomes, ILongDefeatMenuAdapter menu,
        string missionKey, LongDefeatRunRecords? records = null)
    {
        _missionTime = () => Mission.CurrentTime;

        // One registry for the mission: the field adapter records each attacker's role, the effects adapter finds the commander by it.
        var registry = new LongDefeatAgentRegistry();
        _host = new LongDefeatRunHost(plan, session, logger, outcomes, menu, missionKey, () => new LongDefeatBattlefieldAdapter(Mission, logger, registry), records,
            () => new LongDefeatEffectsAdapter(Mission, registry));
    }

    public float GetTotalTeamPower(Team team) => TeamPower;

    /// <summary>
    /// Whether an agent's removal state is a loss the run counts: a kill or a knock-out. A rout or a deletion (a horse, a despawn) is
    /// neither, and must never pay a bounty.
    /// </summary>
    internal static bool IsLoss(AgentState state) => state == AgentState.Killed || state == AgentState.Unconscious;

    public override void AfterStart()
    {
        base.AfterStart();
        LongDefeatMorale.Arm(Mission);
        LongDefeatHookState.Arm(Mission);
        _host.MarkStartDue();
    }

    public override void OnMissionTick(float dt) => _host.Step(dt, _missionTime);

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        if (_host.WatchesBodies)
            _host.OnBodyEvent(affectedAgent, "removed", agentState.ToString());
        _host.OnAgentRemoved(affectedAgent, IsLoss(agentState));
    }

    // Mission.OnAgentDeleted sets the state to Deleted and calls this, and never clears MainAgent (Mission.cs:2982-2996). A remount's faded body is deleted here some frames after it was removed;
    // the callback is kept, with its state, for the sally line (spike S9). Only a mounted class plans a removal, so any other run pays nothing.
    public override void OnAgentDeleted(Agent affectedAgent)
    {
        base.OnAgentDeleted(affectedAgent);
        if (_host.WatchesBodies && affectedAgent != null)
            _host.OnBodyEvent(affectedAgent, "deleted", affectedAgent.State.ToString());
    }

    // OnScoreHit carries the engine's own isBlocked and damagedHp (Mission.cs:5671-5672; OnAgentHit has neither) and may run off the main thread: the volatile want comes first, so a wave
    // with no Morgul Blades costs one read per hit. A mount's blow is its rider's. The victim's health is read here, after the blow wrote it (Agent.cs:5513, :5527), so a killing blow is dropped.
    public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow,
        in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
    {
        base.OnScoreHit(affectedAgent, affectorAgent, attackerWeapon, isBlocked, isSiegeEngineHit, in blow, in collisionData, damagedHp, hitDistance, shotDifficulty);
        if (!_host.WantsHits)
            return;

        var attacker = LongDefeatHitFilter.Attacker(affectorAgent, a => a.IsMount, a => a.RiderAgent);
        if (LongDefeatHitFilter.Admits(affectedAgent, attacker, isBlocked, damagedHp, affectedAgent?.Health ?? 0f))
            _host.OnHit(affectedAgent, attacker!);

        // The sally's contact count wants every hit that touches the lord or his horse, blocked or not, so it takes the raw agents; the director drops it at once outside a sally wave.
        _host.OnContactHit(affectedAgent, affectorAgent, affectedAgent?.Health ?? 0f);
    }

    // The Elven Volley's arrows (phase3-plan.md Slice 6): the hooks read the engine's hit and forward it to the host, which parks it on the main thread.
    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData collisionData)
    {
        base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in collisionData);
        LongDefeatVolleyHooks.AgentHit(_host, affectedAgent, affectorAgent, in affectorWeapon, in blow, in collisionData);
    }

    public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        base.OnMissileHit(attacker, victim, isCanceled, collisionData);
        LongDefeatVolleyHooks.MissileHit(_host, Mission, victim, isCanceled, in collisionData);
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        base.OnAgentFleeing(affectedAgent);
        _host.OnAgentFleeing(affectedAgent);
    }

    // Crossing the boundary reaches RetreatMission (MissionBoundaryCrossingHandler.cs:133-150). EndGame, not EndMission: with the manager
    // ending, the engine shows the main menu instead of the Custom Battle screen under it. The end report is written by the exit that follows.
    public override void OnRetreatMission()
    {
        base.OnRetreatMission();
        _host.Leave();
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        TearDown();
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        TearDown();
    }

    private void TearDown()
    {
        LongDefeatMorale.Disarm(Mission);
        LongDefeatHookState.Disarm(Mission);
        _host.ShutDown();
    }
}
