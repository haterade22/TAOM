namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// One point-in-time memory reading (#386): process footprint (GetProcessMemoryInfo),
// system commit + physical state (GlobalMemoryStatusEx), and the managed heap
// (GC.GetTotalMemory). Plain DTO — all decisions and formatting live in
// MemoryPressureSampler's pure seams.
public readonly struct MemorySample
{
    /// <summary>PROCESS_MEMORY_COUNTERS_EX.PrivateUsage — the process's committed private bytes.</summary>
    public long PrivMb { get; }

    /// <summary>PROCESS_MEMORY_COUNTERS_EX.WorkingSetSize — physical RAM currently resident.</summary>
    public long WsMb { get; }

    /// <summary>GC.GetTotalMemory(false) — managed heap size (no forced collection).</summary>
    public long HeapMb { get; }

    /// <summary>
    /// False when <see cref="HeapMb"/> could not be read and is therefore not a measurement.
    /// The [MemSample]/[MemStation] log lines still render the numeric token (the cross-language
    /// contract with tools/triage_battle_load.py requires an integer there), but any consumer
    /// drawing a CONCLUSION from the heap figure — notably the crash report's managed-vs-native
    /// share — must omit it rather than treat 0 as measured. Under an OOM-shaped crash the
    /// failing read is the reachable case, and 0 there would falsely strengthen exactly the
    /// native-dominance reading the verdict exists to support.
    /// </summary>
    public bool HeapMbValid { get; }

    /// <summary>System-wide commit charge in use (TotalPageFile - AvailPageFile).</summary>
    public long SysCommitUsedMb { get; }

    /// <summary>System-wide commit limit (RAM + pagefile; MEMORYSTATUSEX.ullTotalPageFile).</summary>
    public long SysCommitLimitMb { get; }

    /// <summary>Physical RAM currently available system-wide (MEMORYSTATUSEX.ullAvailPhys).</summary>
    public long AvailPhysMb { get; }

    /// <summary>Total physical RAM installed (MEMORYSTATUSEX.ullTotalPhys).</summary>
    public long TotalPhysMb { get; }

    /// <summary>MEMORYSTATUSEX.dwMemoryLoad — OS-reported physical memory pressure, 0-100.</summary>
    public int MemLoadPercent { get; }

    public MemorySample(
        long privMb,
        long wsMb,
        long heapMb,
        long sysCommitUsedMb,
        long sysCommitLimitMb,
        long availPhysMb,
        long totalPhysMb,
        int memLoadPercent,
        bool heapMbValid = true)
    {
        PrivMb = privMb;
        WsMb = wsMb;
        HeapMb = heapMb;
        SysCommitUsedMb = sysCommitUsedMb;
        SysCommitLimitMb = sysCommitLimitMb;
        AvailPhysMb = availPhysMb;
        TotalPhysMb = totalPhysMb;
        MemLoadPercent = memLoadPercent;
        HeapMbValid = heapMbValid;
    }
}
