using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// Correlator resolution of Harmony replacement frames (issue #339). When a patched method
/// throws, the exception's stack frame holds Harmony's REPLACEMENT method (rendered as
/// <c>Foo_PatchN</c>), and <c>Harmony.GetPatchInfo(replacement)</c> returns null — the player
/// report printed "(no patches)" on all 17 patched frames of the crash stack, exactly the
/// frames whose owners we needed. These tests patch a real method in-process and assert the
/// collector resolves the replacement frame back to the original and lists its patches.
/// </summary>
[TestClass]
public class HarmonyCorrelationCollectorTests
{
    private const string HarmonyId = "taom.tests.harmony-correlation";
    private static Harmony _harmony = null!;

    public static class PatchTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throwing() => throw new InvalidOperationException("correlator test");
    }

    public static class DummyPostfix
    {
        public static void Postfix() { }
    }

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(
            AccessTools.Method(typeof(PatchTarget), nameof(PatchTarget.Throwing)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(DummyPostfix), nameof(DummyPostfix.Postfix))));
    }

    [ClassCleanup]
    public static void Cleanup() => _harmony.UnpatchAll(HarmonyId);

    private static Exception CaptureThrow()
    {
        try { PatchTarget.Throwing(); }
        catch (Exception ex) { return ex; }
        throw new AssertFailedException("PatchTarget.Throwing did not throw.");
    }

    [TestMethod]
    public void CollectFromException_ReplacementFrame_ListsPatchesOfOriginalMethod()
    {
        // Arrange — a real Harmony-patched method whose exception stack holds the replacement.
        var caught = CaptureThrow();
        var raw = new StackTrace(caught, fNeedFileInfo: false).GetFrames();
        Assert.IsNotNull(raw, "Exception stack trace has no frames.");
        var snapshots = raw!
            .Select((f, i) => new StackFrameSnapshot(i, f.GetMethod()?.Name, null, null, null, 0, 0))
            .ToList();

        // Act
        var result = new HarmonyCorrelationCollector().CollectFromException(caught, snapshots);

        // Assert — the throwing frame is the Harmony replacement of Throwing(); the collector
        // must resolve it to the original and list the test postfix under our owner id.
        var throwingFrame = result.PatchesPerStackFrame.FirstOrDefault(
            f => f.MethodFullName != null && f.MethodFullName.Contains(nameof(PatchTarget.Throwing)));
        Assert.IsNotNull(throwingFrame, "No frame for the patched method found in the correlation snapshot.");
        Assert.IsTrue(
            throwingFrame!.Patches.Any(p => p.Owner == HarmonyId && p.PatchType == "Postfix"),
            $"Replacement frame did not resolve to the original method's patches. Patches found: " +
            string.Join(", ", throwingFrame.Patches.Select(p => $"{p.Owner}:{p.PatchType}")));
    }

    [TestMethod]
    public void CollectFromException_UnpatchedFrame_ReportsNoPatches()
    {
        var caught = CaptureThrow();
        var raw = new StackTrace(caught, fNeedFileInfo: false).GetFrames();
        var snapshots = raw!
            .Select((f, i) => new StackFrameSnapshot(i, f.GetMethod()?.Name, null, null, null, 0, 0))
            .ToList();

        var result = new HarmonyCorrelationCollector().CollectFromException(caught, snapshots);

        // CaptureThrow's frame is on the exception stack and unpatched — it must stay
        // patch-free (no false owners from the resolution pass).
        var captureFrame = result.PatchesPerStackFrame.FirstOrDefault(
            f => f.MethodFullName != null && f.MethodFullName.Contains(nameof(CaptureThrow)));
        Assert.IsNotNull(captureFrame, "CaptureThrow frame missing from the correlation snapshot.");
        Assert.AreEqual(0, captureFrame!.Patches.Count, "Unpatched frame must not acquire patches.");
    }
}
