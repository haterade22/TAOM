using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class PlainTextCrashReportRendererTests
{
    [TestMethod]
    public void Render_MinimalContext_ProducesAllSections()
    {
        var ctx = MakeMinimalContext();
        var sut = new PlainTextCrashReportRenderer();

        var text = sut.Render(ctx);

        Assert.IsTrue(text.Contains("--- Identity ---"));
        Assert.IsTrue(text.Contains("--- Exception ---"));
        Assert.IsTrue(text.Contains("--- Stack Frames"));
        Assert.IsTrue(text.Contains("--- Harmony Correlation ---"));
        Assert.IsTrue(text.Contains("--- Modules"));
        Assert.IsTrue(text.Contains("--- Assemblies"));
        Assert.IsTrue(text.Contains("--- Campaign ---"));
        Assert.IsTrue(text.Contains("--- Mission ---"));
        Assert.IsTrue(text.Contains("--- TAOM State ---"));
        Assert.IsTrue(text.Contains("--- MCM Settings"));
        Assert.IsTrue(text.Contains("--- Process ---"));
        Assert.IsTrue(text.Contains("--- GPU"));
        Assert.IsTrue(text.Contains("--- Display ---"));
        Assert.IsTrue(text.Contains("--- OS ---"));
        Assert.IsTrue(text.Contains("--- AppDomain & Environment ---"));
        Assert.IsTrue(text.Contains("--- Performance ---"));
        Assert.IsTrue(text.Contains("--- Logs ---"));
        Assert.IsTrue(text.Contains("--- Collector failures ---"));
    }

    [TestMethod]
    public void Render_HeaderContainsSignature()
    {
        var ctx = MakeMinimalContext() with { CrashSignature = "abc123def456" };
        var sut = new PlainTextCrashReportRenderer();

        var text = sut.Render(ctx);

        Assert.IsTrue(text.Contains("Signature: abc123def456"));
    }

    [TestMethod]
    public void Render_ExceptionAndInnerChain_ShowsBoth()
    {
        var inner = new ExceptionFrame("System.IO.IOException", "inner-msg", "src", 0, "TS", "stack", System.Array.Empty<DataEntry>(), null);
        var outer = new ExceptionFrame("System.Exception", "outer-msg", "src", 0, "TS", "stack", System.Array.Empty<DataEntry>(), inner);
        var ctx = MakeMinimalContext() with { Exception = outer };
        var sut = new PlainTextCrashReportRenderer();

        var text = sut.Render(ctx);

        Assert.IsTrue(text.Contains("outer-msg"));
        Assert.IsTrue(text.Contains("inner-msg"));
        Assert.IsTrue(text.Contains("Inner Exception (depth 1)"));
    }

    [TestMethod]
    public void Render_CollectorFailures_ReportedInTrailingSection()
    {
        var ctx = MakeMinimalContext() with
        {
            CollectorFailures = new[]
            {
                new CollectorFailure("Modules", "InvalidOperationException", "boom"),
                new CollectorFailure("Gpu", "ManagementException", "wmi-unavailable"),
            }
        };
        var sut = new PlainTextCrashReportRenderer();

        var text = sut.Render(ctx);

        Assert.IsTrue(text.Contains("Modules: InvalidOperationException: boom"));
        Assert.IsTrue(text.Contains("Gpu: ManagementException: wmi-unavailable"));
    }

    private static ExceptionContext MakeMinimalContext()
    {
        return new ExceptionContext(
            CapturedAtUtc: DateTime.UtcNow,
            CrashSignature: "deadbeef",
            Identity: new IdentitySnapshot("v1.4.5", "1.4.5.x", "v2.0.0", "sha1", "Some.Origin", "en-US"),
            Exception: null,
            StackFrames: System.Array.Empty<StackFrameSnapshot>(),
            Harmony: new HarmonyCorrelationSnapshot(System.Array.Empty<StackFramePatchInfo>(), System.Array.Empty<HarmonyOwnerSummary>(), 0),
            Modules: new ModuleInventorySnapshot(System.Array.Empty<ModuleSnapshot>()),
            Assemblies: new AssemblyInventorySnapshot(System.Array.Empty<AssemblySnapshot>()),
            Campaign: null,
            Mission: null,
            Taom: new TaomStateSnapshot(null, System.Array.Empty<SpecialResourceEntry>(), null, null, null),
            Mcm: new McmSettingsSnapshot(System.Array.Empty<McmProviderSnapshot>()),
            Process: new ProcessSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0d, new ThrowingThreadSnapshot(1, "test", false, "Unknown")),
            Gpu: new GpuSnapshot(System.Array.Empty<GpuAdapterEntry>()),
            Display: new DisplaySnapshot(1920, 1080, 60, false, 1),
            Os: new OsSnapshot("Windows", "10.0", true, 8, "x64", "en-US", "en-US", "4.0.30319"),
            AppDomain: new AppDomainSnapshot("Test", "C:\\test", null, true),
            EnvVars: System.Array.Empty<EnvVarEntry>(),
            Performance: new FrameTimingSnapshot(System.Array.Empty<float>(), 0d, 0d, 0),
            Logs: new LogTailSnapshot(null, System.Array.Empty<string>(), null, System.Array.Empty<string>()),
            CollectorFailures: System.Array.Empty<CollectorFailure>());
    }
}
