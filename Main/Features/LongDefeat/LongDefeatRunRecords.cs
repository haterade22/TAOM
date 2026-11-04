using System;
using System.IO;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Where a run leaves its books: the best-run file and the JSON report beside the log. The report is null when the logger writes
/// nowhere. An unattended run builds this with its own folder, so it never overwrites the player's best.
/// </summary>
internal sealed class LongDefeatRunRecords
{
    internal LongDefeatRunRecords(LongDefeatBestRunStore best, LongDefeatReportFile? report)
    {
        Best = best;
        Report = report;
    }

    internal LongDefeatBestRunStore Best { get; }

    internal LongDefeatReportFile? Report { get; }

    /// <summary>The player's own folder in Documents, the one the recorder commands write to.</summary>
    internal static readonly string PlayerFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "TAOM");

    internal static LongDefeatRunRecords ForGame(string? logFilePath) => ForFolder(PlayerFolder, logFilePath);

    internal static LongDefeatRunRecords ForFolder(string directory, string? logFilePath) =>
        new(new LongDefeatBestRunStore(directory), LongDefeatReportFile.For(logFilePath));
}
