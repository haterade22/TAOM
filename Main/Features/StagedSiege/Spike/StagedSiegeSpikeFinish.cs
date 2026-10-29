using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>Which mission result a forced finish returns: the attacker wins as "defender pushed back", otherwise a defeat.</summary>
internal static class StagedSiegeSpikeFinish
{
    internal static bool PlayerIsAttacker(Mission mission) => mission.PlayerTeam?.Side == BattleSideEnum.Attacker;

    internal static MissionResult CreateResult(Mission mission) =>
        PlayerIsAttacker(mission) ? MissionResult.CreateDefenderPushedBack() : MissionResult.CreateDefeated(mission);

    internal static string Describe(Mission mission) =>
        PlayerIsAttacker(mission) ? "CreateDefenderPushedBack (player is the attacker)" : "CreateDefeated (player is not the attacker)";
}
