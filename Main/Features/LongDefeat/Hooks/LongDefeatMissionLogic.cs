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
        _host.OnAgentRemoved(affectedAgent, IsLoss(agentState));
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
