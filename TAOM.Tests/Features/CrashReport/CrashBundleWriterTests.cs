using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// Two halves. The manifest half is pure (no disk, no ZIP): the manifest is what a triager reads
/// first, so an OOM-shaped crash has to be visible there without unzipping report.txt. The bundle
/// half pins what actually lands in the ZIP players upload: a file the collector gathers but the
/// writer drops is, in practice, a file that does not exist.
/// </summary>
[TestClass]
public class CrashBundleWriterTests
{
    private const string TestBuildStamp =
        "v2.0.0.0 build.20260923-184249Z+0123456789abcdef0123456789abcdef01234567.dirty";

    private string _dir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TAOM_Bundle_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // ---- manifest (pure) ----

    [TestMethod]
    public void BuildManifest_SystemMemoryPresent_CarriesMemoryVerdictLine()
    {
        var ctx = MakeContext(new SystemMemorySnapshot(
            PrivateMb: 4211, WorkingSetMb: 3900, ManagedHeapMb: 654,
            SysCommitUsedMb: 29847, SysCommitLimitMb: 31646,
            AvailPhysMb: 310, TotalPhysMb: 16296, MemLoadPercent: 97), EmptyLogs());

        var manifest = CrashBundleWriter.BuildManifest(ctx, "report", "{}");

        StringAssert.Contains(manifest, "Memory: MEMORY PRESSURE - privMB=4211");
        StringAssert.Contains(manifest, "headroom 1799MB (5%)");
    }

    [TestMethod]
    public void BuildManifest_SystemMemoryNull_OmitsMemoryLine()
    {
        var manifest = CrashBundleWriter.BuildManifest(MakeContext(null, EmptyLogs()), "report", "{}");

        Assert.IsFalse(manifest.Contains("Memory:"), manifest);
        // The rest of the manifest is unaffected.
        StringAssert.Contains(manifest, "TAOM CrashReport bundle");
        StringAssert.Contains(manifest, "Signature: deadbeef");
    }

    [TestMethod]
    public void BuildManifest_CarriesTheTaomBuildStamp()
    {
        var manifest = CrashBundleWriter.BuildManifest(MakeContext(null, EmptyLogs()), "report", "{}");

        StringAssert.Contains(manifest, "TAOM build: " + TestBuildStamp,
            "the manifest is read first, so the build identity belongs there without unzipping");
    }

    // ---- the ZIP (#481: diag.log travels with the bundle) ----

    [TestMethod]
    public void Write_WithDiagLogPath_PutsDiagLogInTheZip()
    {
        var diag = Path.Combine(_dir, "diag.log");
        File.WriteAllText(diag, "PatchShield swallowed TypeLoadException from a patch on Foo.Bar");

        var ctx = MakeContext(null, new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), diag, new[] { "tail" }));
        var zipPath = new CrashBundleWriter().Write(ctx, "report", "{}", _dir);

        Assert.IsNotNull(zipPath);
        CollectionAssert.Contains(EntryNames(zipPath!), "diag.log",
            "the engine-mismatch evidence has to travel in the bundle players actually upload");
        StringAssert.Contains(ReadEntry(zipPath!, "diag.log"), "TypeLoadException");
    }

    [TestMethod]
    public void Write_WithDiagLogPath_NamesItsSourceInTheManifest()
    {
        var diag = Path.Combine(_dir, "diag.log");
        File.WriteAllText(diag, "diag contents");

        var ctx = MakeContext(null, new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), diag, Array.Empty<string>()));
        var zipPath = new CrashBundleWriter().Write(ctx, "report", "{}", _dir);

        StringAssert.Contains(ReadEntry(zipPath!, "manifest.txt"), "diag.log",
            "the manifest is the inventory a triager reads first");
    }

    [TestMethod]
    public void Write_WithNoDiagLog_StillWritesTheRestOfTheBundle()
    {
        var ctx = MakeContext(null, EmptyLogs());
        var zipPath = new CrashBundleWriter().Write(ctx, "report", "{}", _dir);

        Assert.IsNotNull(zipPath);
        var names = EntryNames(zipPath!);
        CollectionAssert.Contains(names, "report.txt");
        CollectionAssert.Contains(names, "manifest.txt");
        CollectionAssert.DoesNotContain(names, "diag.log", "an absent diag.log must not produce an empty entry");
    }

    // ---- helpers ----

    private static LogTailSnapshot EmptyLogs() =>
        new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), null, Array.Empty<string>());

    private static string[] EntryNames(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.Select(e => e.FullName).ToArray();
    }

    private static string ReadEntry(string zipPath, string entry)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        using var sr = new StreamReader(zip.GetEntry(entry)!.Open());
        return sr.ReadToEnd();
    }

    private static ExceptionContext MakeContext(SystemMemorySnapshot? memory, LogTailSnapshot logs)
    {
        return new ExceptionContext(
            CapturedAtUtc: new DateTime(2026, 8, 19, 19, 4, 50, DateTimeKind.Utc),
            CrashSignature: "deadbeef",
            Identity: new IdentitySnapshot("v1.5.2", "1.5.2.x", "v2.0.28", "sha1", "Some.Origin", "en-US", TestBuildStamp),
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
            Logs: logs,
            CollectorFailures: Array.Empty<CollectorFailure>());
    }
}
