using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using TAOM.Features.CrashReport.BugReport;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Rendering;

// Writes the comprehensive crash bundle ZIP — what we ask players to upload.
// Uses System.IO.Compression.ZipArchive (in-BCL since .NET 4.5; no NuGet add).
// Best-effort: silent skip on any inner failure to keep the report writable.
//
// Bundle filename: taom_crash_{utc}_{sig8}.zip under Logs/
// Contents:
//   report.txt          — full PlainText rendering
//   report.json         — JSON serialisation of ExceptionContext
//   taom_debug.log      — current session's TAOM debug log (if path resolves)
//   rgl_log.txt         — current session's TaleWorlds RGL log tail (if path resolves)
//   diag.log            — TAOM.Dependencies PatchShield log (if path resolves)
//   manifest.txt        — one-line-per-file inventory with sizes and SHA1
public sealed class CrashBundleWriter
{
    public string? Write(
        ExceptionContext context,
        string plainTextReport,
        string jsonReport,
        string outputDirectory)
    {
        string sig8 = context.CrashSignature.Length >= 8 ? context.CrashSignature.Substring(0, 8) : context.CrashSignature;
        return WriteZip(context, plainTextReport, jsonReport, outputDirectory, $"taom_crash_{{0}}_{sig8}.zip", null);
    }

    // The player's bug report (BugReportStartup): the same contents with no crash, plus the previous session's files.
    public string? WriteReport(
        ExceptionContext context,
        string plainTextReport,
        string jsonReport,
        string outputDirectory,
        BugReportSources sources)
    {
        return WriteZip(context, plainTextReport, jsonReport, outputDirectory, "taom_report_{0}.zip", sources);
    }

    private string? WriteZip(
        ExceptionContext context,
        string plainTextReport,
        string jsonReport,
        string outputDirectory,
        string fileNameFormat,
        BugReportSources? sources)
    {
        string zipPath;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            string stamp = context.CapturedAtUtc.ToString("yyyyMMdd_HHmmss");
            zipPath = Path.Combine(outputDirectory, string.Format(fileNameFormat, stamp));
        }
        catch { return null; }

        try
        {
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false);

            WriteText(zip, "report.txt", plainTextReport);
            WriteText(zip, "report.json", jsonReport);
            TryCopyFile(zip, "taom_debug.log", context.Logs?.TaomDebugLogPath);
            TryCopyFile(zip, "rgl_log.txt", context.Logs?.RglLogPath);
            TryCopyFile(zip, "diag.log", context.Logs?.DiagLogPath);
            foreach (var file in sources?.Files ?? Array.Empty<BugReportFile>())
                TryCopyFile(zip, file.EntryName, file.SourcePath);

            var manifest = BuildManifest(context, plainTextReport, jsonReport);
            if (sources != null) manifest = BuildReportManifest(manifest, sources);
            WriteText(zip, "manifest.txt", manifest);
        }
        catch (Exception)
        {
            // Bundle write failed mid-way. The partial .zip is left on disk for forensic
            // inspection (named with `.partial` suffix so a player notified of "broken
            // bundle" can identify it; the original target name is NOT created so callers
            // returning null can be sure no usable bundle exists at the promised path).
            // Codex review #46 (2026-05-25) LOW-02 fix.
            try
            {
                var partialPath = zipPath + ".partial";
                if (File.Exists(zipPath))
                {
                    if (File.Exists(partialPath)) File.Delete(partialPath);
                    File.Move(zipPath, partialPath);
                }
            }
            catch { /* rename best-effort */ }
            return null;
        }
        return zipPath;
    }

    private static void WriteText(ZipArchive zip, string entryName, string text)
    {
        try
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var es = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(text ?? "");
            es.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }

    private static void TryCopyFile(ZipArchive zip, string entryName, string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return;
        try
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var es = entry.Open();
            using var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.CopyTo(es);
        }
        catch { }
    }

    // File names only: the source folder can carry the Windows user name.
    internal static string BuildReportManifest(string crashManifest, BugReportSources sources)
    {
        var sb = new StringBuilder(crashManifest.Length + 512);
        sb.AppendLine("TAOM bug report (made by the player; no crash was caught, so Exception reads (unknown))");
        sb.Append(crashManifest);
        foreach (var file in sources.Files)
        {
            long size = -1;
            try { if (File.Exists(file.SourcePath)) size = new FileInfo(file.SourcePath).Length; } catch { }
            sb.AppendLine(size < 0
                ? $"{file.EntryName}  not found ({Path.GetFileName(file.SourcePath)})"
                : $"{file.EntryName}  {size} bytes from {Path.GetFileName(file.SourcePath)}");
        }
        sb.AppendLine();
        sb.AppendLine("--- Notes ---");
        foreach (var note in sources.Notes) sb.AppendLine(note);
        return sb.ToString();
    }

    // internal (not private) for direct unit testing via InternalsVisibleTo("TAOM.Tests").
    internal static string BuildManifest(ExceptionContext c, string text, string json)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine($"TAOM CrashReport bundle");
        sb.AppendLine($"Captured: {c.CapturedAtUtc:O}");
        sb.AppendLine($"Signature: {c.CrashSignature}");
        sb.AppendLine($"TAOM version: {c.Identity.TaomVersion}");
        sb.AppendLine($"TAOM build: {c.Identity.TaomBuild}");
        sb.AppendLine($"Bannerlord version: {c.Identity.BannerlordVersion}");
        sb.AppendLine($"Origin: {c.Identity.OriginatingPatchTarget}");
        sb.AppendLine($"Exception: {c.Exception?.Type ?? "(unknown)"}");
        // So the ZIP self-describes: triage can see an OOM-shaped crash without unzipping
        // report.txt. Omitted when the reader failed, never a fabricated zero.
        var memoryHeadline = MemoryPressureVerdict.FormatHeadline(c.SystemMemory);
        if (memoryHeadline != null) sb.AppendLine($"Memory: {memoryHeadline}");
        sb.AppendLine();
        sb.AppendLine("--- File inventory ---");
        sb.AppendLine($"report.txt        {text?.Length ?? 0} chars");
        sb.AppendLine($"report.json       {json?.Length ?? 0} chars");
        if (!string.IsNullOrEmpty(c.Logs?.TaomDebugLogPath))
            sb.AppendLine($"taom_debug.log    source={c.Logs.TaomDebugLogPath}");
        if (!string.IsNullOrEmpty(c.Logs?.RglLogPath))
            sb.AppendLine($"rgl_log.txt       source={c.Logs.RglLogPath}");
        if (!string.IsNullOrEmpty(c.Logs?.DiagLogPath))
            sb.AppendLine($"diag.log          source={c.Logs.DiagLogPath}");
        return sb.ToString();
    }
}
