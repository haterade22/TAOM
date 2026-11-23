using SandBox.Missions.MissionLogics.Arena;
using SandBox.Tournaments.MissionLogics;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Which missions may carry signature strikes. Its own type so the MissionLogic stays a thin
/// entry point (ADR-002) and the rule is readable in one place.
///
/// Narrower than DreadMissionGate in one way and wider in another: it does NOT require a campaign
/// (Custom Battle is the cheap smoke route, and the ring needs no campaign context), but it does
/// exclude arenas and tournaments (a slam in a tournament ring would flatten the other entrants,
/// the failure family <c>rca-tournament-exit-hang-2026-07-06.md</c> carries a patch for) and any
/// multiplayer session.
///
/// #1537: the yard and the tournament are known by their own mission behaviours, which the mission's opener attaches
/// before any TAOM behaviour runs. <c>Mission.CombatType</c> cannot tell them apart: nothing in the installed game sets
/// it, so it reads <c>Combat</c> in every mission. An arena quest duel carries neither behaviour and stays open.
/// </summary>
public static class SignatureMissionGate
{
    public static bool IsEligible(Mission? mission) =>
        mission != null && !GameNetwork.IsSessionActive && IsCombatOutsideArena(mission);

    /// <summary>The mission-kind part of the gate, without the multiplayer arm. The dwarf attacks use it too.</summary>
    public static bool IsCombatOutsideArena(Mission mission) =>
        AllowsKind(mission.CombatType == Mission.MissionCombatType.Combat, HasPracticeController(mission), HasTournament(mission));

    /// <summary>True in the town arena's practice mission (practice, team and wave fights) and in a tournament match.</summary>
    public static bool IsArenaOrTournament(Mission mission) => IsArena(HasPracticeController(mission), HasTournament(mission));

    /// <summary>
    /// The rule itself: every mission except the training yard and a tournament match. The combat type stays in it as a
    /// guard only; it is <c>Combat</c> in every mission today, town and conversation scenes included.
    /// </summary>
    public static bool AllowsKind(bool combat, bool practiceYard, bool tournament) => combat && !IsArena(practiceYard, tournament);

    /// <summary>Either marker alone makes the mission an arena: the yard has only the first, a tournament only the second.</summary>
    public static bool IsArena(bool practiceYard, bool tournament) => practiceYard || tournament;

    /// <summary>The decision plus the one log line that lets a replay prove it (#606).</summary>
    public static bool IsEligible(Mission? mission, IModLogger logger)
    {
        var eligible = IsEligible(mission);
        var arena = mission != null && IsArenaOrTournament(mission);
        logger.LogInfo($"[SignatureStrikes] mission gate: eligible={eligible} combatType={mission?.CombatType} arena={arena} multiplayer={GameNetwork.IsSessionActive}");
        return eligible;
    }

    private static bool HasPracticeController(Mission mission) => mission.GetMissionBehavior<ArenaPracticeFightMissionController>() != null;

    private static bool HasTournament(Mission mission) => mission.GetMissionBehavior<TournamentBehavior>() != null;
}
