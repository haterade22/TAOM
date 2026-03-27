using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles;

public class CustomBattleTeamFixBehavior : MissionBehavior
{
    private readonly IModLogger _logger;
    private bool _hasFixedTeams;

    public CustomBattleTeamFixBehavior(IModLogger logger)
    {
        _logger = logger;
    }

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        FixTeamRelationships();
    }

    public override void AfterStart()
    {
        base.AfterStart();
        FixTeamRelationships();
    }

    public override void OnMissionTick(float dt)
    {
        if (!_hasFixedTeams)
            FixTeamRelationships();
    }

    private void FixTeamRelationships()
    {
        if (_hasFixedTeams || Mission == null || Mission.Teams.Count < 2)
            return;

        var defenderTeam = Mission.DefenderTeam;
        var attackerTeam = Mission.AttackerTeam;

        if (defenderTeam == null || attackerTeam == null)
            return;

        if (!defenderTeam.IsEnemyOf(attackerTeam))
        {
            _logger?.LogWarning("CustomBattleTeamFix: Teams are not enemies, forcing enemy relationship");
            defenderTeam.SetIsEnemyOf(attackerTeam, true);
            attackerTeam.SetIsEnemyOf(defenderTeam, true);
        }

        _hasFixedTeams = true;
        _logger?.LogDebug($"CustomBattleTeamFix: Team relationships verified (enemies={defenderTeam.IsEnemyOf(attackerTeam)})");
    }
}
