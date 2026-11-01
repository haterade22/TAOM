using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// The text of the files the autorun leaves for the launcher: <c>fords_autorun_done.txt</c> (one <c>key=value</c> per line, written
/// once) and <c>fords_autorun_status.txt</c> (the heartbeat, rewritten every few seconds). Pure apart from reading the report.
/// </summary>
internal static class FordsAutorunDone
{
    internal const string DoneFile = "fords_autorun_done.txt";
    internal const string StatusFile = "fords_autorun_status.txt";

    internal static string Status(string phase, double wall, FordsRunReport report) =>
        "phase=" + phase + "\nwall=" + F1(wall) + "\nmt=" + F1(report.MissionSeconds) + "\nlive=" + F1(report.Run?.Perf.LiveSeconds ?? 0.0) + "\n";

    internal static string Text(string reason, FordsRunReport report, double wall, string started, string ended)
    {
        var run = report.Run;
        var logPath = report.Logger.LogFilePath;
        var text = new StringBuilder();
        text.Append("reason=").Append(reason).Append('\n');
        text.Append("run=").Append(run?.Run ?? 0).Append('\n');
        text.Append("report=").Append(ReportPath(logPath, run)).Append('\n');
        text.Append("log=").Append(string.IsNullOrEmpty(logPath) ? "none" : Path.GetFullPath(logPath!)).Append('\n');
        text.Append("live_seconds=").Append(F1(run?.Perf.LiveSeconds ?? 0.0)).Append('\n');
        text.Append("wall_seconds=").Append(F1(wall)).Append('\n');
        text.Append("final_passes=").Append(run?.FinalPasses ?? 0).Append('\n');
        text.Append("final_total=").Append(Total(report, run)).Append('\n');
        text.Append("started=").Append(started).Append('\n');
        text.Append("ended=").Append(ended).Append('\n');
        return text.ToString();
    }

    private static string ReportPath(string? logPath, FordsRunEvidence? run) =>
        run == null ? "none" : FordsReportFile.For(logPath)?.PathFor(run.Run) ?? "none";

    private static string Total(FordsRunReport report, FordsRunEvidence? run)
    {
        if (run == null)
            return "none";

        try
        {
            var rows = FordsVerdicts.Judge(report.Session, run);
            int Count(FordsResult result) => rows.Count(r => r.Result == result);
            return "pass=" + Count(FordsResult.Pass) + " fail=" + Count(FordsResult.Fail) + " partial=" + Count(FordsResult.Partial)
                + " notseen=" + Count(FordsResult.NotSeen);
        }
        catch (Exception)
        {
            // swallow-ok: the verdict count is a convenience line in the done file; "unknown" says it could not be made.
            return "unknown";
        }
    }

    internal static string F1(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
