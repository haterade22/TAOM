using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// Entry point for culture doctrines (#608): at <c>EarlyStart</c>, replace each AI team's vanilla
/// tactic list with its culture's and, when a doctrine routes troops into a formation of their
/// own, subscribe the mission's troop-class override; then leave the engine alone. Design:
/// <c>docs/features/culture-doctrine.md</c>. Inherits <see cref="MissionLogic"/>, not
/// <see cref="MissionBehavior"/> (<c>rca-looter-battle-nre-2026-05-24.md</c>).
///
/// <para>
/// Why <c>EarlyStart</c> and nothing later: <c>Mission.AfterStart</c> runs every behavior's
/// <c>EarlyStart</c> in list order (`Mission.cs:3833-3836`) and TAOM's behaviors are appended
/// after the mission's own, so <c>MissionCombatantsLogic.EarlyStart</c> has already created the
/// team AIs, registered vanilla's tactics and set <c>MissionTeamAIType</c>. Its <c>ResetTactic</c>
/// returned before choosing (no formation has units yet, `TeamAIComponent.cs:257`), so the list is
/// swapped before any decision. From the first <c>Team.Tick</c> the list is read on the async AI
/// thread with no lock, so it is never touched again; the MCM toggle is read here and applies
/// from the next battle. No <c>OnBehaviorInitialize</c>: it never fires for TAOM's behaviors (#606).
/// </para>
/// </summary>
public sealed class CultureDoctrineMissionLogic : MissionLogic
{
    private const float StatusIntervalSeconds = 5f;

    private readonly ICultureDoctrineSettingsProvider _settings;
    private readonly ICultureDoctrineConfigProvider _config;
    private readonly IModLogger _logger;
    private readonly List<TaomTacticBase> _taomTactics = new List<TaomTacticBase>();
    private readonly FormationRoutingSubscriber _routing = new FormationRoutingSubscriber();
    private bool _status;
    private float _nextStatusTime;

    public CultureDoctrineMissionLogic()
        : this(IoC.Resolve<ICultureDoctrineSettingsProvider>(), IoC.Resolve<ICultureDoctrineConfigProvider>(), IoC.Resolve<IModLogger>())
    {
    }

    public CultureDoctrineMissionLogic(ICultureDoctrineSettingsProvider settings, ICultureDoctrineConfigProvider config, IModLogger logger)
    {
        _settings = settings;
        _config = config;
        _logger = logger;
    }

    public override void OnCreated() => Reset();

    public override void EarlyStart()
    {
        // Live read: MissionCombatantsLogic.EarlyStart set the type moments ago; never cache it at init.
        if (!Mission.IsFieldBattle)
            return;
        // The status line is the A/B's instrument and runs in the OFF arm too.
        _status = _settings.IsDebug;
        if (!_settings.IsEnabled)
        {
            if (_status)
                _logger.LogInfo("[Doctrine] off: vanilla tactics stay (status line on)");
            return;
        }
        try
        {
            Apply();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Doctrine] disabled for this mission after {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (!_status || Mission.CurrentTime < _nextStatusTime)
            return;
        _nextStatusTime = Mission.CurrentTime + StatusIntervalSeconds;
        try
        {
            _logger.LogInfo(TeamTacticProbe.DescribeAll(Mission, _taomTactics));
        }
        catch (Exception ex)
        {
            _status = false;
            _logger.LogError($"[Doctrine] status line disabled after {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnEndMission() => Reset();

    public override void OnRemoveBehavior()
    {
        Reset();
        base.OnRemoveBehavior();
    }

    private void Apply()
    {
        var combatants = Mission.GetMissionBehavior<MissionCombatantsLogic>();
        if (combatants == null)
        {
            _logger.LogWarning("[Doctrine] no MissionCombatantsLogic in this mission; vanilla tactics stay");
            return;
        }
        var catalog = _config.GetCatalog();
        // Before the teams: the first spawn is on the next mission tick and reads the override.
        _routing.Subscribe(Mission, catalog);
        var teams = Mission.Teams;
        for (var i = 0; i < teams.Count; i++)
        {
            var team = teams[i];
            if (!team.HasTeamAi || team.Side == BattleSideEnum.None)
                continue;
            _logger.LogInfo(TeamDoctrineInstaller.Install(Mission, team, catalog, combatants, _taomTactics));
        }
        if (_routing.IsSubscribed)
            _logger.LogInfo("[Doctrine] formation routing subscribed (GetAgentTroopClass_Override)");
    }

    // Tactic objects hold Team and Formation references, and the routing handler holds the
    // Mission; none may outlive the mission.
    private void Reset()
    {
        _routing.Unsubscribe();
        _taomTactics.Clear();
        _status = false;
        _nextStatusTime = 0f;
    }
}
