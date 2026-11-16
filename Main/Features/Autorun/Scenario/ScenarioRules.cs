using System;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// The pure decisions the scenario driver makes. Every wall-time or day gate is a positive requirement, so a NaN fails it
/// (.claude/rules/csharp-architecture.md "Engine-Float Decision Gates"): a NaN wait expires, a NaN day is never a day.
/// </summary>
internal static class ScenarioRules
{
    internal const double MenuSettleSeconds = 4.0;
    internal const double MapSettleSeconds = 5.0;
    internal const double CreationWaitSeconds = 600.0;
    internal const double MapWaitSeconds = 600.0;
    /// <summary>Under the launcher's 120 s exit grace (run_fords_autorun.py EXIT_GRACE_SECONDS), so the game quits before it is killed.</summary>
    internal const double ExitWaitSeconds = 60.0;
    /// <summary>One wait for every story step: the menu free, the option chosen, the mission open, the map back.</summary>
    internal const double StoryStepWaitSeconds = 120.0;
    internal const double StatusSeconds = 5.0;
    internal const int TitleLogCap = 20;
    internal const int UnknownDay = -1;
    private const double LastDay = 1e9;

    internal static bool MenuSettled(double heldSeconds) => heldSeconds >= MenuSettleSeconds;

    internal static bool MapSettled(double heldSeconds) => heldSeconds >= MapSettleSeconds;

    /// <summary>True while a wait is still inside its limit; a NaN on either side counts as expired.</summary>
    internal static bool Waiting(double sinceSeconds, double limitSeconds) => sinceSeconds < limitSeconds;

    /// <summary>True while the run is at or under the wall limit; a NaN on either side counts as over it.</summary>
    internal static bool WithinWallLimit(double sinceStartSeconds, double limitSeconds) => sinceStartSeconds <= limitSeconds;

    internal static bool StatusDue(double wall, double lastStatus) => wall - lastStatus >= StatusSeconds;

    /// <summary>The done reason a story mission verdict forces, or null when it was decided (Won or Lost).</summary>
    internal static string? MissionReason(StoryOutcome? verdict) => verdict switch
    {
        StoryOutcome.Won or StoryOutcome.Lost => null,
        StoryOutcome.Undecided => "mission_undecided",
        _ => "mission_no_verdict",
    };

    internal static bool OnMap(AutorunScreen screen) => screen == AutorunScreen.Map || screen == AutorunScreen.MapMenu;

    /// <summary>The whole day of the campaign clock, or <see cref="UnknownDay"/> when it is NaN, infinite, negative or absurd.</summary>
    internal static int WholeDay(double elapsedDays) =>
        elapsedDays >= 0.0 && elapsedDays < LastDay ? (int)Math.Floor(elapsedDays) : UnknownDay;

    internal static bool DaysDone(int day, int dayStart, int wanted) =>
        dayStart >= 0 && day >= 0 && day - dayStart >= wanted;

    internal static bool TitleLogged(int hiddenCount) => hiddenCount <= TitleLogCap;

    internal static int DaysRun(int dayStart, int dayEnd) =>
        dayStart >= 0 && dayEnd >= dayStart ? dayEnd - dayStart : 0;
}
