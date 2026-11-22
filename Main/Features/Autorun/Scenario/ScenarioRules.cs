using System;
using System.Collections.Generic;
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
    /// <summary>The offer's popup shows about 30 campaign ticks after the window opens, and a new campaign can show other popups first.</summary>
    internal const double OfferPopupWaitSeconds = 120.0;
    internal const double OfferPopupSettleSeconds = 2.0;
    internal const string ContinueOptionId = "taom_story_offer_continue";
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

    /// <summary>
    /// The offer menu options a choice may take, in the order tried: a fortress window offers defend or attack, a hero story stand or hunt
    /// (StoryOfferBehavior.AddMenu). Empty for a choice the plan never holds.
    /// </summary>
    internal static IReadOnlyList<string> OfferOptionIds(string choice) => choice switch
    {
        ScenarioPlan.ChoiceDefend => new[] { "taom_story_offer_defend", "taom_story_offer_stand" },
        ScenarioPlan.ChoiceAttack => new[] { "taom_story_offer_attack", "taom_story_offer_hunt" },
        ScenarioPlan.ChoiceFate => new[] { "taom_story_offer_decline" },
        _ => Array.Empty<string>(),
    };

    /// <summary>
    /// The answer of <c>taom.chronicle_open &lt;id&gt; confirm</c> when it opened this event's window: FormatOpen's "[WarChronicle] Opened: " and
    /// OpenChronicleEventNow's "'&lt;id&gt;' is Active until day N." An event that resolved at once as text answers "'&lt;id&gt;' Occurred." and has no window.
    /// </summary>
    internal static bool OpenedTheWindow(string output, string eventId) =>
        output.Contains("[WarChronicle] Opened: '" + eventId + "' is Active until day");

    /// <summary>
    /// How long the offer kind waits for the popup: 120 s after an open command. When the window opens by its date, the wall limit less the
    /// mission's share of it, so that a late popup still leaves the mission its whole limit before the runner gives up.
    /// </summary>
    internal static double OfferPopupLimit(bool openByDate, double wallLimitSeconds, double missionLimitSeconds) =>
        openByDate ? System.Math.Max(OfferPopupWaitSeconds, wallLimitSeconds - missionLimitSeconds) : OfferPopupWaitSeconds;

    /// <summary>True once the offer's popup has stayed open long enough to be a settled screen; a NaN never settles.</summary>
    internal static bool PopupSettled(double heldSeconds) => heldSeconds >= OfferPopupSettleSeconds;

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
