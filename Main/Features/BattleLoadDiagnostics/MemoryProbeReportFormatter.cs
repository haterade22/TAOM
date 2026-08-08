using System.Linq;
using System.Text;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Pure rendering for the taom.print_memory probe. All formatting lives here so it is 100% testable
/// without an engine, per ADR-008 — the reader crosses a native boundary and cannot be unit-tested,
/// so everything that CAN be is kept out of it.
///
/// Tag is [MemProbe], deliberately NOT [MemSample]: tools/triage_battle_load.py parses [MemSample]
/// with strict regexes as a cross-language contract, and an operator-triggered, free-form,
/// multi-line block would be a third contract to keep in sync for no benefit. The probe is read by
/// humans filling in the commit-attribution matrix; [MemSample] stays the machine-parsed series.
/// </summary>
public static class MemoryProbeReportFormatter
{
    public const string Tag = "[MemProbe]";

    /// <summary>Max station-label length. Long enough for "B-battle-250v250-run2".</summary>
    internal const int MaxLabelLength = 32;

    /// <summary>
    /// Station labels are echoed into taom_debug, which triage tooling parses line-by-line. An
    /// unvalidated label containing a newline or a bracket could forge a log line, so restrict it to
    /// characters that cannot terminate or fake one. Empty is allowed and means "no label".
    /// </summary>
    internal static bool IsValidLabel(string? label)
    {
        if (string.IsNullOrEmpty(label)) return true;
        if (label!.Length > MaxLabelLength) return false;
        return label.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':' || c == '-');
    }

    /// <param name="label">
    /// Optional station name (e.g. "B-menu") echoed into the header so a matrix run is
    /// self-recording. Null or empty means no label.
    /// </param>
    /// <param name="osRead">
    /// False when the OS reader failed. Checked alongside <paramref name="os"/> having a value, so a
    /// caller that reports success but hands over nothing still renders "unavailable" rather than
    /// a block of zeros.
    /// </param>
    public static string Format(EngineMemoryStats engine, MemorySample? os, bool osRead, string? label = null)
    {
        if (!IsValidLabel(label))
            return $"{Tag} rejected the label: use at most {MaxLabelLength} characters from A-Z a-z 0-9 _ . : -";

        var sb = new StringBuilder();
        var header = string.IsNullOrEmpty(label) ? "reading" : $"reading '{label}'";
        sb.AppendLine($"{Tag} {header}");

        // OS-level view. Omitted wholesale when the reader failed — never a fabricated zero.
        if (osRead && os.HasValue)
        {
            var s = os.Value;
            sb.AppendLine($"{Tag} os: privMB={s.PrivMb} wsMB={s.WsMb} heapMB={s.HeapMb} " +
                          $"sysCommitUsedMB={s.SysCommitUsedMb} sysCommitLimitMB={s.SysCommitLimitMb} " +
                          $"availPhysMB={s.AvailPhysMb} memLoad={s.MemLoadPercent}%");
        }
        else
        {
            sb.AppendLine($"{Tag} os: <unavailable>");
        }

        if (engine.GpuCostRead)
            sb.AppendLine($"{Tag} gpu: estimatedCostMB={engine.GpuCostMb}");
        else
            sb.AppendLine($"{Tag} gpu: <unavailable>");

        if (!string.IsNullOrEmpty(engine.GpuDumpPath))
            sb.AppendLine($"{Tag} gpu dump written: {engine.GpuDumpPath}");

        AppendBlock(sb, "application", engine.ApplicationStatistics);
        AppendBlock(sb, "native", engine.NativeStatistics);

        return sb.ToString().TrimEnd();
    }

    // The engine returns these as multi-line blobs. Every line gets the tag so a grep for [MemProbe]
    // returns the whole reading rather than just its first line.
    private static void AppendBlock(StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            sb.AppendLine($"{Tag} {name}: <unavailable>");
            return;
        }

        sb.AppendLine($"{Tag} {name}:");
        foreach (var line in value!.Replace("\r\n", "\n").Split('\n'))
            sb.AppendLine($"{Tag}   {line}");
    }
}
