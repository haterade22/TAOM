using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// Pins what actually lands in the ZIP players upload. The bundle is the only artifact most reports
/// ever include, so a file the collector gathers but the writer drops is, in practice, a file that
/// does not exist.
/// </summary>
[TestClass]
public class CrashBundleWriterTests
{
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

    private static ExceptionContext ContextWithLogs(LogTailSnapshot logs) =>
        new ExceptionContext(
            CapturedAtUtc: new DateTime(2026, 8, 19, 19, 4, 50, DateTimeKind.Utc),
            CrashSignature: "deadbeefcafe",
            Identity: new IdentitySnapshot("v1.4.7", "1.4.7.x", "v2.0.20", "sha1", "Some.Origin", "en-US"),
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
            Gpu: new GpuSnapshot(Array.Empty<GpuAdapterEntry>()),
            Display: new DisplaySnapshot(1920, 1080, 60, false, 1),
            Os: new OsSnapshot("Windows", "10.0", true, 8, "x64", "en-US", "en-US", "4.0.30319"),
            AppDomain: new AppDomainSnapshot("Test", "C:\test", null, true),
            EnvVars: Array.Empty<EnvVarEntry>(),
            Performance: new FrameTimingSnapshot(Array.Empty<float>(), 0d, 0d, 0),
            Logs: logs,
            CollectorFailures: Array.Empty<CollectorFailure>());

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

    [TestMethod]
    public void Write_WithDiagLogPath_PutsDiagLogInTheZip()
    {
        var diag = Path.Combine(_dir, "diag.log");
        File.WriteAllText(diag, "PatchShield swallowed TypeLoadException from a patch on Foo.Bar");

        var ctx = ContextWithLogs(new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), diag, new[] { "tail" }));
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

        var ctx = ContextWithLogs(new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), diag, Array.Empty<string>()));
        var zipPath = new CrashBundleWriter().Write(ctx, "report", "{}", _dir);

        StringAssert.Contains(ReadEntry(zipPath!, "manifest.txt"), "diag.log",
            "the manifest is the inventory a triager reads first");
    }

    [TestMethod]
    public void Write_WithNoDiagLog_StillWritesTheRestOfTheBundle()
    {
        var ctx = ContextWithLogs(new LogTailSnapshot(null, Array.Empty<string>(), null, Array.Empty<string>(), null, Array.Empty<string>()));
        var zipPath = new CrashBundleWriter().Write(ctx, "report", "{}", _dir);

        Assert.IsNotNull(zipPath);
        var names = EntryNames(zipPath!);
        CollectionAssert.Contains(names, "report.txt");
        CollectionAssert.Contains(names, "manifest.txt");
        CollectionAssert.DoesNotContain(names, "diag.log", "an absent diag.log must not produce an empty entry");
    }
}
