using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Mission-side merit sampler (the donor's cleanest code, ported): every SampleInterval
/// seconds while the player agent is active, sample cohesion (near formation captain),
/// commander proximity (nearest allied hero) and engagement (nearest enemy); count the
/// player's kills; submit ONE sample at mission end. `: MissionLogic` — NEVER
/// MissionBehavior (BehaviorTreeMissionLogic regression rule). Registered UNCONDITIONALLY
/// from SubModule (the donor's mission.Mode gate at init time never fired — Mode is still
/// StartUp there); all filtering happens inside.
/// </summary>
public class EnlistmentMeritMissionBehavior : MissionLogic
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IBattleMeritAccumulator _accumulator;
    private readonly MeritScoringConfig _scoring;

    private bool _active;
    private float _sampleClock;
    private int _samples;
    private int _cohesionHits;
    private int _commanderHits;
    private int _engagementHits;
    private int _kills;
    private float _downSince = -1f;
    private float _missionStart = -1f;
    private bool _fellEarly;

    public EnlistmentMeritMissionBehavior(
        IEnlistmentStateQuery query,
        IBattleMeritAccumulator accumulator,
        MeritScoringConfig scoring)
    {
        _query = query;
        _accumulator = accumulator;
        _scoring = scoring;
    }

    public override void AfterStart()
    {
        // Self-filter: campaign battles during enlisted service only.
        _active = Campaign.Current != null
            && _query.IsEnlisted
            && _query.State == Domain.EnlistmentState.EnlistedBattle;
    }

    public override void OnMissionTick(float dt)
    {
        if (!_active)
            return;
        if (_missionStart < 0f)
            _missionStart = Mission.CurrentTime;

        _sampleClock += dt;
        if (_sampleClock < _scoring.SampleIntervalSeconds)
            return;
        _sampleClock = 0f;

        var main = Mission.MainAgent;
        if (main == null || !main.IsActive())
        {
            if (_downSince < 0f)
            {
                _downSince = Mission.CurrentTime;
                _fellEarly = Mission.CurrentTime - _missionStart < 60f;
            }
            return;
        }

        _samples++;
        SampleDistances(main);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (!_active || affectorAgent == null || !affectorAgent.IsMainAgent)
            return;
        if (affectedAgent?.Team != null && affectorAgent.Team != null && affectedAgent.Team.IsEnemyOf(affectorAgent.Team))
            _kills++;
    }

    protected override void OnEndMission()
    {
        if (!_active)
            return;
        _active = false;

        var survival = _downSince < 0f ? 1f : 0f;
        _accumulator.Submit(new MeritSample
        {
            Kills = _kills,
            SurvivalRatio = survival,
            CohesionRatio = Ratio(_cohesionHits),
            CommanderProximityRatio = Ratio(_commanderHits),
            EngagementRatio = Ratio(_engagementHits),
            FellEarly = _fellEarly,
            RoleFit = false, // role-fit heuristics are a later refinement; scored neutral
        });
    }

    private float Ratio(int hits) => _samples <= 0 ? 0f : (float)hits / _samples;

    private void SampleDistances(Agent main)
    {
        var position = main.Position;
        var cohesionSq = _scoring.CohesionDistance * _scoring.CohesionDistance;
        var commanderSq = _scoring.CommanderDistance * _scoring.CommanderDistance;
        var engagementSq = _scoring.EngagementDistance * _scoring.EngagementDistance;

        var captain = main.Formation?.Captain;
        if (captain != null && captain != main && captain.IsActive()
            && captain.Position.DistanceSquared(position) <= cohesionSq)
        {
            _cohesionHits++;
        }

        var commanderNear = false;
        var enemyNear = false;
        foreach (var agent in Mission.Agents)
        {
            if (agent == main || !agent.IsHuman || !agent.IsActive() || agent.Team == null)
                continue;
            var distanceSq = agent.Position.DistanceSquared(position);
            if (!commanderNear && agent.IsHero && !agent.Team.IsEnemyOf(main.Team) && distanceSq <= commanderSq)
                commanderNear = true;
            else if (!enemyNear && agent.Team.IsEnemyOf(main.Team) && distanceSq <= engagementSq)
                enemyNear = true;
            if (commanderNear && enemyNear)
                break;
        }
        if (commanderNear)
            _commanderHits++;
        if (enemyNear)
            _engagementHits++;
    }
}
