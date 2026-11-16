using System.Globalization;
using System.Text;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>What a finished run knows, for the done file and the outcome line. Days are <see cref="ScenarioRules.UnknownDay"/> when never read.</summary>
internal sealed record ScenarioRunFacts(string Reason, int DayStart, int DayEnd, int InquiriesHidden, int NoticesHidden,
    int EventWindowsClosed, int TimeResets, string Prepared, double WallSeconds, string Started, string Ended)
{
    /// <summary>The story mission verdict (Won, Lost, Undecided), or "-" when none came; only the story kind reads it.</summary>
    public string Verdict { get; init; } = "-";

    public double MissionSeconds { get; init; }
}

/// <summary>
/// The text of the files the autorun leaves for the launcher: <c>autorun_done.txt</c> (one <c>key=value</c> per line, written once;
/// <c>reason=ok</c> is the only success) and <c>autorun_status.txt</c> (the heartbeat, rewritten every few seconds). Pure.
/// </summary>
internal static class ScenarioAutorunDone
{
    internal const string DoneFile = "autorun_done.txt";
    internal const string StatusFile = "autorun_status.txt";
    internal const string ReasonOk = "ok";

    internal static string Status(string phase, double wall, int day, AutorunScreen screen, bool timeLocked) =>
        "phase=" + phase + "\nwall=" + F1(wall) + "\nday=" + day.ToString(CultureInfo.InvariantCulture) + "\nscreen=" + screen
        + "\ntime_locked=" + (timeLocked ? 1 : 0) + "\n";

    internal static string Text(ScenarioPlan plan, ScenarioRunFacts facts)
    {
        var text = new StringBuilder();
        Line(text, "reason", facts.Reason);
        Line(text, "kind", plan.Kind);
        Line(text, "scenario", plan.Scenario);
        Line(text, "culture", plan.Culture);
        Line(text, "creation", "skipped");
        Line(text, "day_start", Int(facts.DayStart));
        Line(text, "day_end", Int(facts.DayEnd));
        var story = plan.Kind == ScenarioPlan.KindStoryMission;
        if (!story)
        {
            Line(text, "days_run", Int(ScenarioRules.DaysRun(facts.DayStart, facts.DayEnd)));
            Line(text, "days_wanted", Int(plan.Days));
        }

        Line(text, "inquiries_hidden", Int(facts.InquiriesHidden));
        Line(text, "notices_hidden", Int(facts.NoticesHidden));
        Line(text, "event_windows_closed", Int(facts.EventWindowsClosed));
        Line(text, "time_resets", Int(facts.TimeResets));
        Line(text, "prepared", facts.Prepared.Replace('\r', ' ').Replace('\n', ' '));
        Line(text, "wall_seconds", F1(facts.WallSeconds));
        Line(text, "started", facts.Started);
        Line(text, "ended", facts.Ended);
        if (story)
        {
            Line(text, "mission", plan.MissionId);
            Line(text, "verdict", facts.Verdict);
            Line(text, "mission_seconds", F1(facts.MissionSeconds));
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, string key, string value) => text.Append(key).Append('=').Append(value).Append('\n');

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string F1(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
