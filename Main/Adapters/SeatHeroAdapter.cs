using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Adapters;

/// <summary>The Seat's writes to the hero's agent (build-spec 5.5 and section 8): stand fast, the hand-off to the AI and the hand-back, holding no state.</summary>
internal sealed class SeatHeroAdapter : ISeatHeroAdapter
{
    private readonly IModLogger _logger;

    public SeatHeroAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public void StandFast()
    {
        MissionThreadGuard.NoteCall("SeatHeroAdapter.StandFast", _logger.LogError);

        var agent = Mission.Current?.MainAgent;
        if (agent == null || !agent.IsActive())
            return;

        agent.MovementFlags = default;
        agent.MovementInputVector = Vec2.Zero;
        agent.EventControlFlags = default;
        agent.HeadCameraMode = false;
    }

    public SeatFightRefusal BeginFighting()
    {
        MissionThreadGuard.NoteCall("SeatHeroAdapter.BeginFighting", _logger.LogError);

        var mission = Mission.Current;
        var agent = mission?.MainAgent;
        var active = agent != null && agent.IsActive();
        var facts = new SeatFightFacts(mission == null || mission.MissionEnded, mission == null || mission.IsMissionEnding, active,
            active && agent!.Controller == AgentControllerType.Player);
        var refusal = SeatFightRule.BeginRefusal(facts);
        if (refusal != SeatFightRefusal.None)
            return refusal;

        try
        {
            HandToTheAi(mission!, agent!);
        }
        catch
        {
            if (agent!.Controller == AgentControllerType.AI)
                HandBack(agent);
            throw;
        }

        return SeatFightRefusal.None;
    }

    public void EndFighting()
    {
        MissionThreadGuard.NoteCall("SeatHeroAdapter.EndFighting", _logger.LogError);

        if (HeroOnAi())
            HandBack(Mission.Current.MainAgent);
    }

    public bool HeroActive()
    {
        var agent = Mission.Current?.MainAgent;
        return agent != null && agent.IsActive();
    }

    public bool HeroOnAi()
    {
        var agent = Mission.Current?.MainAgent;
        return agent != null && agent.IsActive() && agent.Controller == AgentControllerType.AI;
    }

    // The component's stop acts only on its own flag, and a native retreat can outlive a hand-back, so the agent's own stop follows. True means he reads as stopped.
    public bool StopHeroRetreat()
    {
        MissionThreadGuard.NoteCall("SeatHeroAdapter.StopHeroRetreat", _logger.LogError);

        var agent = Mission.Current?.MainAgent;
        if (agent == null || !agent.IsActive() || !agent.IsRetreating())
            return false;

        agent.StopRetreatingMoraleComponent();
        if (agent.IsRetreating())
            agent.StopRetreating();
        return !agent.IsRetreating();
    }

    // No morale is read or written around the flip: a Player hero has no CommonAIComponent, so GetMorale answers -1 and a write of it would
    // clamp to 0, where the morale refusal pins him at 0.01 instead of the 50 the new component holds (CommonAIComponent.cs:21, 84-93).
    // Called from the mission tick, never from a Mission.OnMainAgentChanged handler (the setter raises that event from its own body).
    private static void HandToTheAi(Mission mission, Agent agent)
    {
        if (agent.IsUsingGameObject)
            agent.StopUsingGameObject(isSuccessful: false);

        agent.Controller = AgentControllerType.AI;

        // The setter adds CanRide on the way to Player and never removes it on the way to the AI, so a foot hero would hunt for horses.
        if (!agent.HasMount && (agent.SpawnEquipment == null || agent.SpawnEquipment[EquipmentIndex.Horse].IsEmpty))
            agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRide);

        // Vanilla's own hand-over at the end of deployment alarms the agent, unpauses it and refreshes its behaviours
        // (DeploymentMissionController.cs:57-69). Any mode but Battle is left alone; in Battle mode these steps run whether or not the battle had a deployment.
        if (mission.Mode != MissionMode.Battle)
            return;

        agent.SetWatchState(Agent.WatchState.Alarmed);
        agent.SetIsAIPaused(false);
        agent.DisableScriptedMovement();
        var formation = agent.Formation;
        if (formation == null)
            return;

        agent.SetRidingOrder(formation.RidingOrder.OrderEnum);

        // The formation tells a unit that joins to refresh its behaviours for the order it holds (Formation.AddUnit, AttachUnit), but
        // the hero is already in its arrangement, so only the order's own join step is called. A Retreat order would retreat him at once.
        var order = formation.GetReadonlyMovementOrderReference();
        if (order.OrderEnum != MovementOrder.MovementOrderEnum.Retreat)
            order.OnUnitJoinOrLeave(formation, agent, true);
    }

    // The order matters: StopUsingGameObjectAux ends an AI walk or defence, disables the AI use and disables scripted movement only while
    // IsAIControlled holds (Agent.cs:3989, 4004, 4014, 4027), so after the flip it would skip that cleanup. The two delegates the
    // HumanAIComponent constructor subscribed (HumanAIComponent.cs:136-139) are never cleared by its removal, so a second hand-off would
    // pile up handlers. The flip leaves the native retreat mode alone (IMBAgent.SetController), so a retreat begun under the AI is stopped after it.
    private static void HandBack(Agent agent)
    {
        try
        {
            agent.OnAgentWieldedItemChange = null;
            agent.OnAgentMountedStateChanged = null;
            agent.StopUsingGameObject(isSuccessful: false, Agent.StopUsingGameObjectFlags.None);
        }
        finally
        {
            agent.Controller = AgentControllerType.Player;
            if (agent.IsRetreating())
                agent.StopRetreating();
            agent.AIStateFlags = Agent.AIStateFlag.None;
            agent.ClearTargetFrame();
        }
    }
}
