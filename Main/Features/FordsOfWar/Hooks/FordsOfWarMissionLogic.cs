using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The mission boundary of Fords of War (build-spec section 4): builds the three adapters and the <see cref="FordsMissionDirector"/>
/// in <c>AfterStart</c>, ticks it, forwards agent removals, and ends the mission once a keep has fallen. Everything else is the
/// director. The launcher adds this logic to the mission it opens, so there is no behavior declaration. MUST be <c>: MissionLogic</c>
/// (rca-looter-battle-nre-2026-05-24.md). It never spawns <c>Game.Current.PlayerTroop</c> and never gives an agent the Player
/// controller: the mission keeps no main agent.
///
/// <c>AfterStart</c> runs after the combatants logic created the two teams and the boundary placer added <c>walk_area</c>. Each override
/// sits in a try: an exception leaving a start callback restarts the mission load every frame (#699). A match that cannot start logs
/// an error and ends the mission through <c>MissionEnded</c>, which the engine polls every 0.1 s (Mission.cs:4819-4883). Nothing
/// closes a mission ended that way, so the tick also drives <see cref="FordsMissionExit"/>: the way back works without the panel.
/// </summary>
public sealed class FordsOfWarMissionLogic : MissionLogic
{
    private const int MaxFaultsLogged = 5;

    private readonly FordsMatchSetup _setup;
    private readonly FordsConfigCatalog _catalog;
    private readonly FordsSession _session;
    private readonly IModLogger _logger;
    private readonly Diagnostics.FordsRunReport _report;
    private readonly FordsMissionExit _exit;
    private FordsMissionDirector? _director;
    private bool _failed;
    private int _faults;

    internal FordsOfWarMissionLogic(FordsMatchSetup setup, FordsConfigCatalog catalog, FordsSession session, IModLogger logger, Diagnostics.FordsRunReport report, FordsMissionExit? exit = null)
    {
        _setup = setup;
        _catalog = catalog;
        _session = session;
        _logger = logger;
        _report = report;
        _exit = exit ?? new FordsMissionExit(new FordsMenuAdapter(), logger, report);
    }

    public override void AfterStart()
    {
        base.AfterStart();
        try
        {
            var plan = FordsMatchPlan.Resolve(_setup, _catalog, _logger);
            if (plan == null)
            {
                _failed = true;
                return;
            }

            var towers = new FordsTowerAdapter(Mission, _logger, plan.Config.Towers.FireRange);
            var battlefield = new FordsBattlefieldAdapter(Mission, new FordsAgentRegistry(), towers.EntityOf, _logger);
            var director = new FordsMissionDirector(plan, battlefield, towers, new FordsSceneAdapter(Mission, _logger), _session,
                _logger, new SeededFordsRandom(Environment.TickCount), _report);
            _failed = !director.Start();
            if (_failed)
            {
                director.Shutdown();
                return;
            }

            _director = director;
            FordsTowerHitPatch.Install(director);
        }
        catch (Exception ex)
        {
            _failed = true;
            Fault("AfterStart", ex);
        }
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            _director?.Tick(dt);
        }
        catch (Exception ex)
        {
            Fault("OnMissionTick", ex);
        }

        _exit.Tick(dt, _failed, _director?.Winner is { });
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        try
        {
            _director?.OnAgentRemoved(affectedAgent, affectorAgent, agentState == AgentState.Killed || agentState == AgentState.Unconscious);
        }
        catch (Exception ex)
        {
            Fault("OnAgentRemoved", ex);
        }
    }

    public override bool MissionEnded(ref MissionResult missionResult)
    {
        if (!_failed && _director?.Winner is not { })
            return false;

        missionResult = FordsMissionResults.For(Mission, !_failed && _director?.Winner == MatchSide.West);
        return true;
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        var director = _director;
        _director = null;
        if (director == null)
            return;

        try
        {
            FordsTowerHitPatch.Uninstall(director);
            director.Shutdown();
        }
        catch (Exception ex)
        {
            Fault("OnEndMission", ex);
        }
    }

    private void Fault(string where, Exception ex)
    {
        if (++_faults <= MaxFaultsLogged)
            _logger.LogError($"[FordsOfWar] {where} failed ({ex.GetType().Name}: {ex.Message})\n{ex.StackTrace}");
    }
}
