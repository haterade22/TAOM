using System;
using System.IO;
using System.Text;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>Reads the last 256 KB of the prior session's log. Best-effort: any failure is no evidence.</summary>
internal static class StallEvidenceReader
{
    private const int TailBytes = 256 * 1024;

    public static StallEvidence? TryRead(string? logPath)
    {
        if (string.IsNullOrEmpty(logPath)) return null;
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0L, stream.Length - TailBytes);
            stream.Position = start;
            var bytes = new byte[(int)(stream.Length - start)];
            int read = stream.Read(bytes, 0, bytes.Length);
            return StallEvidence.Parse(Encoding.UTF8.GetString(bytes, 0, read));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
