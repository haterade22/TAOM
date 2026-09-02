using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class ProcessEnvironmentCollector
{
    private static readonly string[] EnvVarPrefixes = { "BANNERLORD_", "TAOM_", "DOTNET_", "STEAM_" };

    public ProcessSnapshot CollectProcess()
    {
        long ws = 0, pm = 0, gc = 0; int g0 = 0, g1 = 0, g2 = 0, pt = 0; double uptime = 0;
        try { var p = Process.GetCurrentProcess(); ws = p.WorkingSet64; pm = p.PrivateMemorySize64; pt = p.Threads?.Count ?? 0; uptime = (DateTime.UtcNow - p.StartTime.ToUniversalTime()).TotalSeconds; } catch { }
        try { gc = GC.GetTotalMemory(forceFullCollection: false); } catch { }
        try { g0 = GC.CollectionCount(0); } catch { }
        try { g1 = GC.CollectionCount(1); } catch { }
        try { g2 = GC.CollectionCount(2); } catch { }
        int managedThreads = 0;
        try { managedThreads = Process.GetCurrentProcess().Threads.Count; } catch { }

        var throwing = CollectThrowingThread();
        return new ProcessSnapshot(ws, pm, gc, g0, g1, g2, pt, managedThreads, uptime, throwing);
    }

    /// <summary>
    /// System-wide commit + physical state (#385 follow-up). Returns null when the reader fails,
    /// so the renderer omits the section rather than printing zeroes.
    /// </summary>
    /// <remarks>
    /// Reads through <c>MemorySampleReader</c>, which lives in the BattleLoadDiagnostics
    /// namespace but the same assembly. That cross-feature <c>internal</c> call is deliberate:
    /// it is the single implementation of the GlobalMemoryStatusEx + GetProcessMemoryInfo pair,
    /// it is the same reader whose output the bundled taom_debug.log already carries as
    /// [MemSample] lines, and duplicating it here would be a second place for the P/Invoke
    /// marshalling to drift. Note this collector's every other read is an untestable static too
    /// (Process, GC, Environment, RuntimeInformation), so an interface over one of them would
    /// abstract nothing; the testable logic lives in MemoryPressureVerdict instead.
    /// </remarks>
    public SystemMemorySnapshot? CollectSystemMemory()
    {
        if (!MemorySampleReader.TryRead(out var s)) return null;
        return new SystemMemorySnapshot(
            PrivateMb: s.PrivMb,
            WorkingSetMb: s.WsMb,
            // Null, not 0, when the read failed: a fabricated 0 here would read as
            // "managed 0% of private" and falsely strengthen the native-dominance conclusion.
            ManagedHeapMb: s.HeapMbValid ? s.HeapMb : (long?)null,
            SysCommitUsedMb: s.SysCommitUsedMb,
            SysCommitLimitMb: s.SysCommitLimitMb,
            AvailPhysMb: s.AvailPhysMb,
            TotalPhysMb: s.TotalPhysMb,
            MemLoadPercent: s.MemLoadPercent);
    }

    private static ThrowingThreadSnapshot CollectThrowingThread()
    {
        int id = 0; string? name = null; bool bg = false; string apt = "Unknown";
        try { var t = Thread.CurrentThread; id = t.ManagedThreadId; name = t.Name; bg = t.IsBackground; apt = t.GetApartmentState().ToString(); } catch { }
        return new ThrowingThreadSnapshot(id, name, bg, apt);
    }

    public OsSnapshot CollectOs()
    {
        string desc = "(unknown)"; string version = "(unknown)"; bool is64 = false; int cores = 0; string arch = "(unknown)";
        string culture = "(unknown)"; string uic = "(unknown)"; string clr = "(unknown)";
        try { desc = System.Runtime.InteropServices.RuntimeInformation.OSDescription; } catch { }
        try { version = Environment.OSVersion.VersionString; } catch { }
        try { is64 = Environment.Is64BitOperatingSystem; } catch { }
        try { cores = Environment.ProcessorCount; } catch { }
        try { arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(); } catch { }
        try { culture = CultureInfo.CurrentCulture.Name; } catch { }
        try { uic = CultureInfo.CurrentUICulture.Name; } catch { }
        try { clr = Environment.Version.ToString(); } catch { }
        return new OsSnapshot(desc, version, is64, cores, arch, culture, uic, clr);
    }

    public AppDomainSnapshot CollectAppDomain()
    {
        string friendly = "(unknown)"; string baseDir = "(unknown)"; string? rsp = null; bool trusted = false;
        try { var d = AppDomain.CurrentDomain; friendly = d.FriendlyName; baseDir = d.BaseDirectory; rsp = d.RelativeSearchPath; trusted = d.IsFullyTrusted; } catch { }
        return new AppDomainSnapshot(friendly, baseDir, rsp, trusted);
    }

    public IReadOnlyList<EnvVarEntry> CollectEnvVars()
    {
        var list = new List<EnvVarEntry>();
        try
        {
            foreach (System.Collections.DictionaryEntry kv in Environment.GetEnvironmentVariables())
            {
                var key = kv.Key?.ToString() ?? "";
                if (string.IsNullOrEmpty(key)) continue;
                if (!EnvVarPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new EnvVarEntry(key, kv.Value?.ToString() ?? ""));
            }
        }
        catch { }
        return list.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }
}
