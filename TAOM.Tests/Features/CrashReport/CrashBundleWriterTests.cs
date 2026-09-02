using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Tests.Features.CrashReport;

// Pure manifest-building tests only — no disk I/O, no ZIP. The manifest is what a triager reads
// first, so an OOM-shaped crash has to be visible there without unzipping report.txt.
[TestClass]
public class CrashBundleWriterTests
{
    [TestMethod]
    public void BuildManifest_SystemMemoryPresent_CarriesMemoryVerdictLine()
    {
        var ctx = MakeContext(new SystemMemorySnapshot(
            PrivateMb: 4211, WorkingSetMb: 3900, ManagedHeapMb: 654,
            SysCommitUsedMb: 29847, SysCommitLimitMb: 31646,
            AvailPhysMb: 310, TotalPhysMb: 16296, MemLoadPercent: 97));

        var manifest = CrashBundleWriter.BuildManifest(ctx, "report", "{}");

        StringAssert.Contains(manifest, "Memory: MEMORY PRESSURE - privMB=4211");
        StringAssert.Contains(manifest, "headroom 1799MB (5%)");
    }

    [TestMethod]
    public void BuildManifest_SystemMemoryNull_OmitsMemoryLine()
    {
        var manifest = CrashBundleWriter.BuildManifest(MakeContext(null), "report", "{}");

        Assert.IsFalse(manifest.Contains("Memory:"), manifest);
        // The rest of the manifest is unaffected.
        StringAssert.Contains(manifest, "TAOM CrashReport bundle");
        StringAssert.Contains(manifest, "Signature: deadbeef");
    }

    private static ExceptionContext MakeContext(SystemMemorySnapshot? memory)
    {
        return new ExceptionContext(
            CapturedAtUtc: DateTime.UtcNow,
            CrashSignature: "deadbeef",
            Identity: new IdentitySnapshot("v1.4.8", "1.4.8.x", "v2.0.23", "sha1", "Some.Origin", "en-US"),
            Exception: null,
            StackFrames: Array.Empty<StackFrameSnapshot>(),
            Harmony: new HarmonyCorrelationSnapshot(Array.Empty<StackFramePatchInfo>(), Array.Empty<HarmonyOwnerSummary>(), 0),
            Modules: new ModuleInventorySnapshot(Array.Empty<ModuleSnapshot>()),
            Assemblies: new AssemblyInventorySnapshot(Array.Empty<AssemblySnapshot>()),
            Campaign: null,
            Mission: null,
            Taom: new TaomStateSnapshot(null, Array.Empty<SpecialResourceEntry>(), null, null, null),
            Mcm: new McmSettingsSnapshot(Array.Empty<McmProviderSnapshot>()),
            Process: new ProcessSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0d, new ThrowingThreadSnapshot(1, "test", false, "Unknown")),
            SystemMemory: memory,
            Gpu: new GpuSnapshot(Array.Empty<GpuAdapterEntry>()),
            Display: new DisplaySnapshot(1920, 1080, 60, false, 1),
            Os: new OsSnapshot("Windows", "10.0", true, 8, "x64", "en-US", "en-US", "4.0.30319"),
            AppDomain: new AppDomainSnapshot("Test", "C:\\test", null, true),
            EnvVars: Array.Empty<EnvVarEntry>(),
            Performance: new FrameTimingSnapshot(Array.Empty<float>(), 0d, 0d, 0),
            Logs: new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>()),
            CollectorFailures: Array.Empty<CollectorFailure>());
    }
}
