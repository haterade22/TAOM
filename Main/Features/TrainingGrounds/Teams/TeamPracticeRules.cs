using System;
using System.Collections.Generic;

namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: the pure rules of a team practice fight at the training grounds (docs/features/team-practice.md).
/// Vanilla keeps six AI teams in <c>_AIParticipantTeams</c>; in a team fight they split by index. The first
/// <see cref="AllyTeamCount"/> teams fight beside the player, the rest are the other side. No engine type.
/// </summary>
public static class TeamPracticeRules
{
    /// <summary>
    /// How many of the six AI teams fight on the player's side: the player and two allies face four. One named
    /// constant, so the maintainer can change the odds in one place. Half the teams (three) would make vanilla's
    /// top prize, 250 denars, easier to win than it is today.
    /// </summary>
    public const int AllyTeamCount = 2;

    /// <summary>The blue outline an ally wears while the player practises (ARGB).</summary>
    public const uint AllyOutline = 0xFF4F8FD9u;

    /// <summary>
    /// True for an AI team on the player's side. A list of <see cref="AllyTeamCount"/> teams or fewer has no ally,
    /// so a short list never makes everyone an ally and no one an opponent.
    /// </summary>
    public static bool IsAllyTeam(int aiIndex, int aiCount) =>
        aiCount > AllyTeamCount && aiIndex >= 0 && aiIndex < AllyTeamCount;

    /// <summary>
    /// True when two AI teams fight each other: only teams of different sides do. A list with no ally side keeps
    /// vanilla's rule, where every team is the enemy of every other.
    /// </summary>
    public static bool AreEnemies(int a, int b, int aiCount) =>
        a != b && (aiCount <= AllyTeamCount || IsAllyTeam(a, aiCount) != IsAllyTeam(b, aiCount));

    /// <summary>
    /// True when the player's side has won: the player stands, every fighter has entered the yard
    /// (<paramref name="unspawned"/> is 0), someone is still alive and no team of the other side has a fighter
    /// left. <paramref name="alive"/> at 0 is vanilla's own win, so it is left to vanilla.
    /// </summary>
    public static bool IsTeamWin(bool playerStanding, int unspawned, int alive, bool anyOpponentStanding) =>
        playerStanding && unspawned <= 0 && alive > 0 && !anyOpponentStanding;

    /// <summary>
    /// True when the practice fight runs as a team fight: the reflection access to vanilla's private fields resolved
    /// and the town is a team town. Without the access the patches cannot run the fight, so the mode is "not teams" and
    /// the Training Master never describes a team fight that did not happen. The town is asked only when the access holds.
    /// </summary>
    public static bool IsTeamMode(bool accessAvailable, Func<bool> townIsTeamTown) =>
        accessAvailable && townIsTeamTown();

    /// <summary>
    /// True when the Training Master says the team win debrief: a team fight, no fighter left to enter or stand (the count
    /// vanilla pays 250 denars on) and the player on his feet, because the text says he was still standing.
    /// </summary>
    public static bool ShowsTeamWin(bool teams, int remainingFromLastPractice, bool playerSurvived) =>
        teams && remainingFromLastPractice == 0 && playerSurvived;

    /// <summary>
    /// True when a team of the other side still has a fighter. <paramref name="hasBots"/> holds one flag for each AI team
    /// by index; ally indices are skipped. An empty list is no data, so it answers true: a win is never declared without it.
    /// </summary>
    public static bool AnyOpponentStanding(IReadOnlyList<bool> hasBots)
    {
        if (hasBots.Count == 0)
            return true;
        for (var i = 0; i < hasBots.Count; i++)
            if (!IsAllyTeam(i, hasBots.Count) && hasBots[i])
                return true;
        return false;
    }

    /// <summary>
    /// True when the count the team win wrote has slipped below zero: the player won and still stood, every fighter has entered,
    /// and an ally fell after the win was declared (an arrow already in flight might do that in the seconds before the return;
    /// the source does not prove it). Vanilla pays the top prize only at exactly zero, so the count is put back.
    /// </summary>
    public static bool WinCountSlipped(bool playerSurvived, int unspawned, int alive) =>
        playerSurvived && unspawned <= 0 && alive < 0;

    /// <summary>
    /// The number the practice HUD shows as "remaining opponents" in a team fight: vanilla's count minus the allies
    /// still standing, never below 0. Vanilla's count is the fighters still to enter plus every fighter in the yard, so
    /// it holds the player's allies too. A fighter who has not entered has no side yet, so the result is exact once every
    /// fighter has entered and an upper bound before that. After the team win the count is 0 and the allies stand, so the
    /// subtraction would go negative: it stops at 0. A negative allies value counts as 0.
    /// </summary>
    public static int OpponentsLeft(int remaining, int alliesStanding) =>
        Math.Max(0, remaining - Math.Max(0, alliesStanding));

    /// <summary>True when a fighter of this AI team wears the ally outline.</summary>
    public static bool ShouldOutline(bool practising, int aiIndex, int aiCount) =>
        practising && IsAllyTeam(aiIndex, aiCount);
}
