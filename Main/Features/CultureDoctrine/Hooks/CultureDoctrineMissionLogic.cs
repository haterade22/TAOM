using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// Entry point for culture doctrines (#608): at <c>EarlyStart</c>, replace each AI team's vanilla
/// tactic list with its culture's, then leave the engine alone. Design:
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

    private void Apply()
    {
        var combatants = Mission.GetMissionBehavior<MissionCombatantsLogic>();
        if (combatants == null)
        {
            _logger.LogWarning("[Doctrine] no MissionCombatantsLogic in this mission; vanilla tactics stay");
            return;
        }
        var catalog = _config.GetCatalog();
        var teams = Mission.Teams;
        for (var i = 0; i < teams.Count; i++)
        {
            var team = teams[i];
            if (!team.HasTeamAi || team.Side == BattleSideEnum.None)
                continue;
            ApplyToTeam(team, catalog, combatants);
        }
    }

    private void ApplyToTeam(Team team, DoctrineCatalog catalog, MissionCombatantsLogic combatants)
    {
        var selection = TeamCombatantSelector.Select(Mission, combatants, team);
        var profile = SideProfile.From(selection.Combatants, selection.SideTacticsSkill);
        var doctrine = catalog.Resolve(profile.CultureId);
        var side = team.Side == BattleSideEnum.Attacker ? DoctrineSide.Attacker : DoctrineSide.Defender;
        var roster = TacticRoster.Build(doctrine, profile.TacticsSkill, side, CaravanTacticsRule.Ensured(Mission, team.Side));

        // Build the whole list before touching the team, so a construction failure leaves
        // vanilla's list intact rather than an emptied one.
        var tactics = TacticFactory.CreateAll(roster, team);
        team.ClearTacticOptions();
        for (var i = 0; i < tactics.Count; i++)
        {
            team.AddTacticOption(tactics[i]);
            if (tactics[i] is TaomTacticBase taom)
                _taomTactics.Add(taom);
        }
        team.ResetTactic();

        var registered = string.Join(", ", roster.Select(r => r.Tactic + "*" + r.Multiplier.ToString("0.00", CultureInfo.InvariantCulture)));
        _logger.LogInfo($"[Doctrine] team={team.TeamIndex} side={team.Side} player={(team.IsPlayerTeam ? "yes" : "no")} culture={profile.CultureId ?? "none"} doctrine={doctrine.CultureId} troops={profile.TroopCount} tacticsSkill={profile.TacticsSkill} registered=[{registered}]");
    }

    // Tactic objects hold Team and Formation references; none may outlive the mission.
    private void Reset()
    {
        _taomTactics.Clear();
        _status = false;
        _nextStatusTime = 0f;
    }
}
