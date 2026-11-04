using System.Globalization;
using System.Text;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Autorun;

/// <summary>
/// The text of the files the autorun leaves for the launcher: <c>long_defeat_done.txt</c> (one <c>key=value</c> per line, written
/// once) and <c>long_defeat_status.txt</c> (the heartbeat, rewritten every few seconds). The names are the ones
/// <c>tools/run_long_defeat.py</c> polls for. Pure apart from reading the session.
/// </summary>
internal static class LongDefeatAutorunDone
{
    internal const string DoneFile = "long_defeat_done.txt";
    internal const string StatusFile = "long_defeat_status.txt";

    /// <summary>The done file's <c>reason</c> for a run that ended by its own rules; the judge compares these words.</summary>
    internal static string ReasonFor(RunEndReason reason) =>
        reason switch
        {
            RunEndReason.TargetReached => "final",
            RunEndReason.DefendersDead => "lost_defenders",
            RunEndReason.KeepLost => "lost_keep",
            RunEndReason.Stalemate => "stalemate",
            _ => "abandoned",
        };

    internal static string Status(string phase, double wall, LongDefeatSession session)
    {
        var view = session.GetView();
        return "phase=" + phase + "\nwall=" + F1(wall) + "\nlive=" + F1(session.LiveSeconds) + "\nwave=" + (view?.Wave ?? 0)
            + "\ncleared=" + (view?.WavesCleared ?? session.Result?.WavesCleared ?? 0) + "\n";
    }

    internal static string Text(string reason, LongDefeatSession session, string? logPath, string? expect, double wall, string started, string ended)
    {
        var result = session.Result;
        var text = new StringBuilder();
        text.Append("reason=").Append(reason).Append('\n');
        text.Append("report=").Append(session.ReportPath ?? "none").Append('\n');
        text.Append("log=").Append(string.IsNullOrEmpty(logPath) ? "none" : FullPath(logPath!)).Append('\n');
        text.Append("live_seconds=").Append(F1(session.LiveSeconds)).Append('\n');
        text.Append("wall_seconds=").Append(F1(wall)).Append('\n');
        text.Append("result=").Append(result == null ? "none" : result.Reason.ToString()).Append('\n');
        text.Append("waves_cleared=").Append(result?.WavesCleared ?? 0).Append('\n');
        text.Append("kills=").Append(result?.Kills ?? 0).Append('\n');
        text.Append("expect=").Append(expect ?? "none").Append('\n');
        text.Append("started=").Append(started).Append('\n');
        text.Append("ended=").Append(ended).Append('\n');
        return text.ToString();
    }

    private static string FullPath(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (System.Exception)
        {
            return path;
        }
    }

    internal static string F1(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
