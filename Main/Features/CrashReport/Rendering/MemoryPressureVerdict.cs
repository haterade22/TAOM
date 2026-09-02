using System.Text;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Rendering;

/// <summary>
/// Turns a <see cref="SystemMemorySnapshot"/> into the crash report's memory headline and its
/// System Memory section (#385 follow-up). Pure: every seam takes plain values and returns a
/// string, so all of it is unit-tested per ADR-008.
/// </summary>
/// <remarks>
/// <para>
/// <b>The threshold is not defined here.</b> <see cref="IsUnderPressure"/> delegates to
/// <see cref="MemoryPressureSampler.IsLowHeadroom"/>, which owns the low-headroom contract that
/// the Python triage mirror also cites. A deep review already caught that C#/Python pair drifting
/// in the integer-floor band; a THIRD copy of 2048/10 is exactly how that recurs.
/// </para>
/// <para>
/// <b>Native-vs-managed is reported, not classified.</b> The headline carries
/// <c>(managed N% of private)</c> rather than a "native-dominant" label, because labelling it
/// would mean inventing a ratio threshold nobody can defend. Same discipline as
/// <c>TableauDiagnostics.LogRenderCensus</c>, which states observations after an earlier version
/// asserted a conclusion that was later refuted. "managed 3%" reads as native-dominant to any
/// reader, and carries no threshold to drift.
/// </para>
/// </remarks>
public static class MemoryPressureVerdict
{
    /// <summary>
    /// True when commit headroom is under the sampler's low-headroom threshold. Garbage inputs
    /// (non-positive limit, negative used) report false: a verdict must never be computed from
    /// garbage, the same polarity rule the sampler applies to its WARN.
    /// </summary>
    public static bool IsUnderPressure(SystemMemorySnapshot? snapshot)
        => snapshot != null
           && MemoryPressureSampler.IsLowHeadroom(snapshot.SysCommitUsedMb, snapshot.SysCommitLimitMb);

    /// <summary>
    /// The one-line header verdict, or <c>null</c> when the reader failed (caller omits the line
    /// entirely rather than printing zeroes).
    /// </summary>
    public static string? FormatHeadline(SystemMemorySnapshot? s)
    {
        if (s == null) return null;

        var sb = new StringBuilder(160);
        // Three states, not two. Suppressing the derived numbers for a rejected commit reading
        // and then still printing "no memory pressure" turns a nonsensical input into a
        // confident healthy verdict: nothing about an invalid reading establishes that headroom
        // is above the threshold. Absence of evidence is not evidence of health.
        sb.Append(!HasUsableCommit(s) ? "MEMORY STATUS UNKNOWN - invalid commit reading"
                  : IsUnderPressure(s) ? "MEMORY PRESSURE"
                  : "no memory pressure");
        sb.Append(" - privMB=").Append(s.PrivateMb)
          .Append(" wsMB=").Append(s.WorkingSetMb)
          .Append(" heapMB=").Append(s.ManagedHeapMb.HasValue
              ? s.ManagedHeapMb.Value.ToString()
              : "<unavailable>");

        int? managedPct = s.ManagedHeapMb.HasValue ? PercentOf(s.ManagedHeapMb.Value, s.PrivateMb) : null;
        if (managedPct.HasValue)
            sb.Append(" (managed ").Append(managedPct.Value).Append("% of private)");

        // Same garbage guard IsLowHeadroom applies, and for the same reason. Mirroring it in
        // the DECISION but not in the RENDER produced a headline that contradicted itself:
        // sysCommitUsedMb is computed as limit-avail, so a reading where avail exceeds total goes
        // negative, and this clause then printed "headroom 31647MB (100%)" (larger than the limit)
        // right beside the "no memory pressure" label the guard had correctly produced.
        if (HasUsableCommit(s))
        {
            sb.Append(", commit ").Append(s.SysCommitUsedMb).Append('/').Append(s.SysCommitLimitMb).Append("MB");

            long headroom = HeadroomMb(s);
            int? headroomPct = PercentOf(headroom, s.SysCommitLimitMb);
            sb.Append(", headroom ").Append(headroom).Append("MB");
            if (headroomPct.HasValue) sb.Append(" (").Append(headroomPct.Value).Append("%)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The multi-line body of the report's <c>--- System Memory ---</c> section. Never fabricates
    /// a value: a failed read renders as an explicit unavailable line.
    /// </summary>
    public static string FormatDetail(SystemMemorySnapshot? s)
    {
        if (s == null)
        {
            return "(unavailable - GlobalMemoryStatusEx / GetProcessMemoryInfo did not return; "
                   + "no values fabricated)";
        }

        var sb = new StringBuilder(700);

        sb.Append("Process:   privMB=").Append(s.PrivateMb)
          .Append(" wsMB=").Append(s.WorkingSetMb)
          .Append(" heapMB=").Append(s.ManagedHeapMb.HasValue
              ? s.ManagedHeapMb.Value.ToString()
              : "<unavailable>");
        int? managedPct = s.ManagedHeapMb.HasValue ? PercentOf(s.ManagedHeapMb.Value, s.PrivateMb) : null;
        if (managedPct.HasValue) sb.Append("   (managed ").Append(managedPct.Value).Append("% of private)");
        sb.AppendLine();

        sb.Append("Commit:    sysCommitUsedMB=").Append(s.SysCommitUsedMb)
          .Append(" sysCommitLimitMB=").Append(s.SysCommitLimitMb);
        if (HasUsableCommit(s))
        {
            long headroom = HeadroomMb(s);
            sb.Append(" headroomMB=").Append(headroom);
            int? headroomPct = PercentOf(headroom, s.SysCommitLimitMb);
            if (headroomPct.HasValue) sb.Append(" (").Append(headroomPct.Value).Append("%)");
        }
        sb.AppendLine();

        sb.Append("Physical:  availPhysMB=").Append(s.AvailPhysMb)
          .Append(" totalPhysMB=").Append(s.TotalPhysMb)
          .Append(" memLoad=").Append(s.MemLoadPercent).Append('%')
          .AppendLine();

        if (!HasUsableCommit(s))
        {
            sb.AppendLine("Verdict:   MEMORY STATUS UNKNOWN - the commit pair above is not a usable");
            sb.AppendLine("           reading (non-positive limit, or a negative used derived from");
            sb.AppendLine("           limit-minus-available), so no headroom verdict can be drawn from");
            sb.AppendLine("           it. This is NOT a statement that memory was healthy. Read the");
            sb.AppendLine("           [MemSample] trajectory in taom_debug.log instead.");
        }
        else if (IsUnderPressure(s))
        {
            sb.AppendLine("Verdict:   MEMORY PRESSURE - commit headroom is under MemoryPressureSampler's");
            sb.AppendLine("           low-headroom threshold (the larger of 2048 MB and 10% of the limit). A");
            sb.AppendLine("           native allocation failing here surfaces as an access violation far from");
            sb.AppendLine("           its cause, so the exception above may be a symptom rather than the fault.");
            sb.AppendLine("           Cross-check the [MemSample] trajectory in taom_debug.log (also in this");
            sb.AppendLine("           bundle) with tools/triage_battle_load.py.");
        }
        else
        {
            sb.AppendLine("Verdict:   no memory pressure - commit headroom is above the low-headroom threshold.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether the commit pair is worth rendering at all. A non-positive limit makes commit and
    /// headroom meaningless; a NEGATIVE used is a garbage reading, and
    /// <see cref="MemoryPressureSampler.IsLowHeadroom"/> already refuses to compute a verdict from
    /// it. The render path has to refuse for the same reason, or the report shows numbers the
    /// verdict beside them was computed to ignore.
    /// </summary>
    internal static bool HasUsableCommit(SystemMemorySnapshot s)
        => s.SysCommitLimitMb > 0 && s.SysCommitUsedMb >= 0;

    // Only ever called behind HasUsableCommit, so both operands are non-negative and the
    // subtraction cannot overflow. Over-committed (used > limit) is a legitimate reading and
    // clamps to 0, matching MemoryPressureSampler's documented semantic.
    private static long HeadroomMb(SystemMemorySnapshot s)
    {
        long headroom = s.SysCommitLimitMb - s.SysCommitUsedMb;
        return headroom < 0 ? 0 : headroom;
    }

    /// <summary>
    /// Integer percentage, or <c>null</c> when it cannot be computed honestly.
    /// </summary>
    /// <remarks>
    /// The overflow guard is not theoretical hygiene. C# integer arithmetic here is unchecked (no
    /// <c>CheckForOverflowUnderflow</c> in the build), so <c>part * 100</c> silently wraps, and a
    /// wrapped product divided by a large denominator lands on <b>0</b> — a fabricated zero
    /// indistinguishable from a real 0%, reached through arithmetic rather than through a null.
    /// That is the one route this class's omit-on-failure discipline did not otherwise cover, and
    /// the inputs are a P/Invoke struct read taken next to a crash, which is exactly where a
    /// corrupt value would come from.
    /// </remarks>
    internal static int? PercentOf(long part, long whole)
    {
        if (whole <= 0 || part < 0) return null;
        if (part > long.MaxValue / 100) return null; // the multiply below would wrap
        long percent = part * 100 / whole;
        return percent > int.MaxValue ? (int?)null : (int)percent;
    }
}
