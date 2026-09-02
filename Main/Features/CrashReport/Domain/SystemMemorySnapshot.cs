namespace TAOM.Features.CrashReport.Domain;

/// <summary>
/// System-wide commit + physical memory state at capture time (#385 follow-up).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a NULLABLE SIBLING of <see cref="ProcessSnapshot"/> rather than extra fields on
/// it. <c>ProcessSnapshot</c> has a <c>?? new ProcessSnapshot(0, 0, ...)</c> fallback in
/// CrashReportService, so widening it would force a fabricated <c>0</c> into every one of these
/// fields whenever the reader failed, and the renderer could not then tell <c>availPhysMB=0</c>
/// (a real and alarming reading) from "never read". A null sibling renders as
/// <c>(unavailable)</c> instead, which is the omit-on-failure discipline the three sibling sites
/// already enforce (MemStats, FormatFinishWaitDetail, MemoryProbeReportFormatter).
/// </para>
/// <para>
/// Field names and units mirror <c>MemorySample</c> and the <c>[MemSample]</c> log tokens on
/// purpose: one <c>grep privMB</c> over an unzipped bundle then hits the crash report, the
/// manifest, the periodic log trend and the per-phase [BattleLoad] lines.
/// </para>
/// <para>
/// #385 was diagnosed BY the commit figure (20.3 GB against a 31.6 GB limit on a 16 GB machine)
/// and the bundle carried none of it: only WorkingSet64 and PrivateMemorySize64, no commit, no
/// headroom, no physical state.
/// </para>
/// </remarks>
public sealed record SystemMemorySnapshot(
    long PrivateMb,
    long WorkingSetMb,
    /// <summary>Null when the managed-heap read failed. Never 0-as-if-measured.</summary>
    long? ManagedHeapMb,
    long SysCommitUsedMb,
    long SysCommitLimitMb,
    long AvailPhysMb,
    long TotalPhysMb,
    int MemLoadPercent);
