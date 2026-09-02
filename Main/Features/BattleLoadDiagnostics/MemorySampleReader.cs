using System;
using System.Runtime.InteropServices;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Features.BattleLoadDiagnostics;

// Raw memory counters via direct P/Invoke (#386). Deliberately NOT System.Diagnostics.Process:
// Process.PrivateMemorySize64 re-snapshots the whole process table per call — measured
// 7,711 us/call on net472 vs 84 us for this GlobalMemoryStatusEx + GetProcessMemoryInfo pair,
// and this runs on a 5s timer for the whole session. Every path is try/catch'd and returns
// false on failure — a diagnostics reader must never throw into a timer callback.
internal static class MemorySampleReader
{
    private const long BytesPerMb = 1024 * 1024;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;   // system commit LIMIT (RAM + pagefile)
        public ulong ullAvailPageFile;   // commit headroom remaining
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    // 2 x uint + 9 x SIZE_T = 80 bytes on x64 (Bannerlord is x64-only).
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;     // the privMB source (committed private bytes)
    }

    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // Pseudo-handle (-1): does not need — and must not get — CloseHandle.
    [DllImport("kernel32")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX counters, uint cb);

    /// <summary>
    /// Full system + process + managed-heap reading. Returns false (sample = default) on any
    /// failure; never throws.
    /// </summary>
    public static bool TryRead(out MemorySample sample)
    {
        sample = default;
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref status)) return false;

            if (!TryReadProcess(out long privMb, out long wsMb)) return false;

            // A failed heap read is NOT a heap of zero. Keep the rest of the sample (the commit
            // figures are the crash-relevant ones and must not be discarded over this), but record
            // that the heap component is not a measurement so consumers can omit it.
            long heapMb;
            bool heapMbValid;
            try
            {
                heapMb = GC.GetTotalMemory(forceFullCollection: false) / BytesPerMb;
                heapMbValid = true;
            }
            catch
            {
                heapMb = 0;
                heapMbValid = false;
            }

            long limitMb = (long)(status.ullTotalPageFile / (ulong)BytesPerMb);
            long availPageFileMb = (long)(status.ullAvailPageFile / (ulong)BytesPerMb);

            sample = new MemorySample(
                privMb: privMb,
                wsMb: wsMb,
                heapMb: heapMb,
                sysCommitUsedMb: limitMb - availPageFileMb,
                sysCommitLimitMb: limitMb,
                availPhysMb: (long)(status.ullAvailPhys / (ulong)BytesPerMb),
                totalPhysMb: (long)(status.ullTotalPhys / (ulong)BytesPerMb),
                memLoadPercent: (int)status.dwMemoryLoad,
                heapMbValid: heapMbValid);
            return true;
        }
        catch
        {
            sample = default;
            return false;
        }
    }

    /// <summary>
    /// Cheap process-only subset (GetProcessMemoryInfo, one syscall) for the per-phase
    /// [BattleLoad] MemStats tokens. Returns false (both 0) on any failure; never throws.
    /// </summary>
    public static bool TryReadProcess(out long privMb, out long wsMb)
    {
        privMb = 0;
        wsMb = 0;
        try
        {
            uint cb = (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS_EX));
            if (!GetProcessMemoryInfo(GetCurrentProcess(), out var counters, cb)) return false;

            privMb = (long)(counters.PrivateUsage.ToUInt64() / (ulong)BytesPerMb);
            wsMb = (long)(counters.WorkingSetSize.ToUInt64() / (ulong)BytesPerMb);
            return true;
        }
        catch
        {
            privMb = 0;
            wsMb = 0;
            return false;
        }
    }
}
