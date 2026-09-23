using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// A Harmony finalizer that hands its exception back makes the generated wrapper execute
/// <c>throw &lt;result&gt;</c>, and throwing an existing exception object replaces its stack trace.
/// PatchShield wraps every patched method in the process, so every crash crossing one of them
/// reached the crash report with the frames below the patched method erased. Player bundle
/// 2d446100 showed a whole childbirth failure as five frames ending at
/// <c>MapState.OnTick_Patch2</c>.
///
/// These tests patch real methods in-process: the premise that the plain rethrow loses the
/// throw-site frame, and that <see cref="RethrowStackPreserver"/> and PatchShield's own finalizers
/// keep it across one and two rethrows.
/// </summary>
[TestClass]
public class RethrowStackPreserverTests
{
    private const string HarmonyId = "taom.tests.rethrow-stack-preserver";
    private static Harmony _harmony = null!;

    private static readonly Regex MarkerLine = new(
        @"--- End of stack trace from previous location \(rethrown by a Harmony finalizer on [^)]*\) ---");

    public static class Targets
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DeepThrowSite() => throw new InvalidOperationException("rethrow test");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PlainRethrow() => DeepThrowSite();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PreservingRethrow() => DeepThrowSite();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShieldedVoid() => DeepThrowSite();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int ShieldedWithResult()
        {
            DeepThrowSite();
            return 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShieldedOuter() => ShieldedVoid();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ReporterAndShield() => DeepThrowSite();

        // Seven frames deep, to pin how many the recorded site keeps.
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level1() => Level2();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level2() => Level3();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level3() => Level4();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level4() => Level5();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level5() => Level6();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level6() => Level7();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Level7() => throw new InvalidOperationException("deep");
    }

    /// <summary>
    /// Stands in for the crash reporter's Patch37 finalizers: priority 800, swallows, and records
    /// what it could see at that moment.
    /// </summary>
    public static class Reporter
    {
        public static int FramesSeen = -1;
        public static string? TraceSeen;

        public static Exception? Finalizer(Exception __exception)
        {
            if (__exception == null) return null;
            FramesSeen = new System.Diagnostics.StackTrace(__exception, false).FrameCount;
            TraceSeen = __exception.StackTrace;
            return null;
        }
    }

    public static class Finalizers
    {
        public static Exception? Plain(Exception __exception) => __exception;

        public static Exception? Preserving(MethodBase __originalMethod, Exception __exception)
        {
            RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
            return __exception;
        }
    }

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _harmony = new Harmony(HarmonyId);
        Patch(nameof(Targets.PlainRethrow), AccessTools.Method(typeof(Finalizers), nameof(Finalizers.Plain)));
        Patch(nameof(Targets.PreservingRethrow), AccessTools.Method(typeof(Finalizers), nameof(Finalizers.Preserving)));

        // PatchShield's REAL finalizers, attached exactly as PatchShield.Install attaches them.
        Patch(nameof(Targets.ShieldedVoid), AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid"));
        Patch(nameof(Targets.ShieldedOuter), AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid"));
        Patch(nameof(Targets.ShieldedWithResult), AccessTools.Method(typeof(PatchShield), "ShieldFinalizerWithResult"));

        // The Module.OnApplicationTick shape: the crash reporter (800) and PatchShield (default 400)
        // on one method.
        var shared = AccessTools.Method(typeof(Targets), nameof(Targets.ReporterAndShield));
        _harmony.Patch(shared, finalizer: new HarmonyMethod(AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid")));
        _harmony.Patch(shared, finalizer: new HarmonyMethod(AccessTools.Method(typeof(Reporter), nameof(Reporter.Finalizer))) { priority = 800 });
    }

    private static void Patch(string target, MethodInfo finalizer)
    {
        Assert.IsNotNull(finalizer, $"finalizer for {target} did not resolve");
        _harmony.Patch(AccessTools.Method(typeof(Targets), target), finalizer: new HarmonyMethod(finalizer));
    }

    [ClassCleanup]
    public static void Cleanup() => _harmony.UnpatchAll(HarmonyId);

    private static Exception Capture(Action call)
    {
        try { call(); }
        catch (Exception ex) { return ex; }
        throw new AssertFailedException("the target did not throw");
    }

    // ---- The premise ---------------------------------------------------------------------------

    [TestMethod]
    public void HarmonyRethrow_FinalizerReturnsException_LosesTheThrowSiteFrame()
    {
        // Pins WHY the preserver exists. If a Harmony upgrade ever rethrows with the stack intact,
        // this fails, and the preserver becomes redundant rather than silently double-counting.
        var ex = Capture(Targets.PlainRethrow);

        StringAssert.DoesNotMatch(ex.StackTrace, new Regex(nameof(Targets.DeepThrowSite)),
            "premise: Harmony's `throw <finalizer result>` resets the trace to the patched frame");
    }

    // ---- The helper through a real Harmony rethrow ---------------------------------------------

    [TestMethod]
    public void PreserveForRethrow_HarmonyRethrow_KeepsTheThrowSiteFrame()
    {
        var ex = Capture(Targets.PreservingRethrow);

        StringAssert.Contains(ex.StackTrace, nameof(Targets.DeepThrowSite));
        Assert.AreEqual(1, MarkerLine.Matches(ex.StackTrace).Count, ex.StackTrace);
    }

    [TestMethod]
    public void PreserveForRethrow_HarmonyRethrow_MarkerNamesTheRethrowingMethod()
    {
        var ex = Capture(Targets.PreservingRethrow);

        StringAssert.Contains(MarkerLine.Match(ex.StackTrace).Value, nameof(Targets.PreservingRethrow));
    }

    [TestMethod]
    public void PreserveForRethrow_HarmonyRethrow_RecordsTheThrowSiteInData()
    {
        var ex = Capture(Targets.PreservingRethrow);

        var site = ex.Data[RethrowStackPreserver.ThrowSiteDataKey] as string;
        Assert.IsNotNull(site, "the throw site must be recorded for the crash signature");
        StringAssert.StartsWith(site, typeof(Targets).FullName + "." + nameof(Targets.DeepThrowSite));
    }

    // ---- PatchShield's real finalizers ---------------------------------------------------------

    [TestMethod]
    public void ShieldFinalizerVoid_NonTrinityException_RethrowsWithTheThrowSiteFrame()
    {
        // The player bundle's shape: a plain exception PatchShield does not swallow.
        var ex = Capture(Targets.ShieldedVoid);

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException), "a non-trinity exception must still propagate");
        StringAssert.Contains(ex.StackTrace, nameof(Targets.DeepThrowSite));
    }

    [TestMethod]
    public void ShieldFinalizerWithResult_NonTrinityException_RethrowsWithTheThrowSiteFrame()
    {
        var ex = Capture(() => Targets.ShieldedWithResult());

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, nameof(Targets.DeepThrowSite));
    }

    [TestMethod]
    public void ShieldFinalizer_NestedShieldedMethods_KeepsEveryFrameAndOneMarkerPerRethrow()
    {
        // MapState.OnTick inside Module.OnApplicationTick, both shielded: two rethrows, and the
        // frames between each pair must all survive.
        var ex = Capture(Targets.ShieldedOuter);

        StringAssert.Contains(ex.StackTrace, nameof(Targets.DeepThrowSite));
        StringAssert.Contains(ex.StackTrace, nameof(Targets.ShieldedVoid));
        StringAssert.Contains(ex.StackTrace, nameof(Targets.ShieldedOuter));
        Assert.AreEqual(2, MarkerLine.Matches(ex.StackTrace).Count, ex.StackTrace);
    }

    [TestMethod]
    public void ShieldFinalizer_NestedShieldedMethods_KeepsTheInnermostThrowSite()
    {
        var ex = Capture(Targets.ShieldedOuter);

        StringAssert.StartsWith(
            (string)ex.Data[RethrowStackPreserver.ThrowSiteDataKey],
            typeof(Targets).FullName + "." + nameof(Targets.DeepThrowSite),
            "the outer rethrow must not overwrite the site the inner one recorded");
    }

    [TestMethod]
    public void CrashReporterFinalizer_SharesAMethodWithPatchShield_SeesLiveFramesBeforeThePreserve()
    {
        // Harmony runs finalizers highest priority first. The reporter must read the exception while
        // its frames are live: after PreserveForRethrow the live trace is null until the next throw,
        // so a reporter running second would build empty Stack Frames and Harmony sections.
        Reporter.FramesSeen = -1;
        Reporter.TraceSeen = null;

        Targets.ReporterAndShield();   // the reporter swallows, as Patch37 does

        Assert.IsTrue(Reporter.FramesSeen > 0, "the reporter saw no live frames");
        StringAssert.Contains(Reporter.TraceSeen, nameof(Targets.DeepThrowSite));
    }

    [TestMethod]
    public void CrashReporterFinalizers_OutrankPatchShield()
    {
        // Pins the real configuration the ordering test above depends on.
        var reporter = typeof(TAOM.Features.CrashReport.Hooks.ModuleOnApplicationTickFinalizer)
            .GetMethod("Finalizer", BindingFlags.NonPublic | BindingFlags.Static);
        var priority = reporter!.GetCustomAttribute<HarmonyPriority>()?.info.priority;

        Assert.IsTrue(priority > HarmonyLib.Priority.Normal,
            $"the crash reporter's finalizers must run before PatchShield's (Priority.Normal); found {priority}");
    }

    [TestMethod]
    public void PreserveForRethrow_DeepStack_RecordsExactlyFiveFramesInnermostFirst()
    {
        // The recorded site is hashed into every shielded crash signature, so its length and join are
        // part of every signature in the wild: changing either re-keys them all.
        var ex = Capture(Targets.Level1);

        RethrowStackPreserver.PreserveForRethrow(ex, null);

        var frames = ((string)ex.Data[RethrowStackPreserver.ThrowSiteDataKey]).Split(new[] { " <- " }, StringSplitOptions.None);
        Assert.AreEqual(5, frames.Length, string.Join(" | ", frames));
        StringAssert.EndsWith(frames[0], "." + nameof(Targets.Level7));
        StringAssert.EndsWith(frames[4], "." + nameof(Targets.Level3));
    }

    // ---- Direct calls (no Harmony) -------------------------------------------------------------

    [TestMethod]
    public void PreserveForRethrow_ReturnsTheSameInstance()
    {
        // Never a wrapper: the exception TYPE a caller above the shield catches must not change.
        var ex = Capture(Targets.DeepThrowSite);

        Assert.AreSame(ex, RethrowStackPreserver.PreserveForRethrow(ex, null));
        Assert.IsNull(RethrowStackPreserver.PreserveForRethrow(null, null));
    }

    [TestMethod]
    public void PreserveForRethrow_NullException_DoesNothing()
    {
        RethrowStackPreserver.PreserveForRethrow(null, null);
    }

    [TestMethod]
    public void PreserveForRethrow_NeverThrownException_LeavesItUntouched()
    {
        var ex = new InvalidOperationException("never thrown");

        RethrowStackPreserver.PreserveForRethrow(ex, null);

        Assert.IsNull(ex.StackTrace);
        Assert.IsFalse(ex.Data.Contains(RethrowStackPreserver.ThrowSiteDataKey));
    }

    [TestMethod]
    public void PreserveForRethrow_CalledTwiceBeforeOneRethrow_AddsOneMarker()
    {
        // Two TAOM finalizers on one method both calling the helper must not stack two markers:
        // Harmony throws once per method, however many finalizers it has.
        var original = Capture(Targets.DeepThrowSite);
        RethrowStackPreserver.PreserveForRethrow(original, null);
        RethrowStackPreserver.PreserveForRethrow(original, null);

        var ex = Capture(() => Rethrow(original));

        StringAssert.Contains(ex.StackTrace, nameof(Targets.DeepThrowSite));
        Assert.AreEqual(1, MarkerLine.Matches(ex.StackTrace).Count, ex.StackTrace);
    }

    [TestMethod]
    public void PreserveForRethrow_NotRethrownAfterwards_TraceHasNoDuplicatedFrames()
    {
        // A later finalizer may swallow instead of rethrowing; the object must still read cleanly.
        var ex = Capture(Targets.DeepThrowSite);

        RethrowStackPreserver.PreserveForRethrow(ex, null);

        Assert.AreEqual(1, Regex.Matches(ex.StackTrace, nameof(Targets.DeepThrowSite)).Count, ex.StackTrace);
    }

    [TestMethod]
    public void PreserveForRethrow_NullSite_MarkerSaysUnknown()
    {
        var original = Capture(Targets.DeepThrowSite);
        RethrowStackPreserver.PreserveForRethrow(original, null);

        var ex = Capture(() => Rethrow(original));

        StringAssert.Contains(MarkerLine.Match(ex.StackTrace).Value, "an unknown method");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Rethrow(Exception ex) => throw ex;

    // ---- Every shield rethrow path calls the helper (IL call presence) --------------------------

    [TestMethod]
    public void ShieldFinalizers_EveryRethrowingFinalizer_CallsPreserveForRethrow()
    {
        // Call presence proves the call has not been deleted, not which branch reaches it; the
        // behavioural tests above cover PatchShield's branches end to end.
        var preserve = typeof(RethrowStackPreserver).GetMethod(nameof(RethrowStackPreserver.PreserveForRethrow));
        Assert.IsNotNull(preserve);
        foreach (var (type, name) in new[]
                 {
                     (typeof(PatchShield), "ShieldFinalizerVoid"),
                     (typeof(PatchShield), "ShieldFinalizerWithResult"),
                     (typeof(SaveShield), "SaveShieldFinalizer"),
                 })
        {
            var method = AccessTools.Method(type, name);
            Assert.IsNotNull(method, $"{type.Name}.{name} did not resolve");
            var il = method.GetMethodBody()!.GetILAsByteArray();
            var calls = IlCallScanner.ExtractCalledMethods(method, il).ToList();
            Assert.IsTrue(calls.Contains(preserve), $"{type.Name}.{name} must call RethrowStackPreserver.PreserveForRethrow");
        }
    }

    [TestMethod]
    public void ShieldFinalizer_NonTrinityRethrow_IsCountedApartFromSwallows()
    {
        // The preserver's cost lands only on exceptions crossing a shield, and how often that happens
        // in normal play is decided by the modlist. The session summary counts them so the cost is
        // visible in a player's log (deep-review efficiency finding 1). Counters are process-global:
        // assert the delta.
        var rethrownBefore = PatchShield.RethrownCount;
        var swallowedBefore = PatchShield.SwallowedTotal;

        Capture(Targets.ShieldedVoid);

        Assert.AreEqual(1, PatchShield.RethrownCount - rethrownBefore);
        Assert.AreEqual(0, PatchShield.SwallowedTotal - swallowedBefore, "a rethrow must not count as a swallow");
    }
}
