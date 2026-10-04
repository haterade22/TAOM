using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

// Fully qualified alias: this namespace nests under TAOM, so a bare SubModule binds to Main's TAOM.SubModule.
using DepsSubModule = TAOM.Dependencies.SubModule;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// PatchShield's finalizers through real Harmony patches: what a shielded method does after a
/// missing-API exception (swallow, diag.log line, strip the foreign owner) and after any other
/// exception (rethrow with a marker naming the shielded method). Plan 034 moved where the finalizers
/// get the shielded method from; these pin that nothing they produce changed.
/// </summary>
[TestClass]
public class PatchShieldFinalizerTests
{
    // The shield's finalizers sit under a "taom" owner, which PatchShield never strips; the throwing
    // prefix belongs to an owner no protected prefix matches, so the rescue may strip it.
    private const string ShieldOwner = "taom.tests.patchshield-finalizer";
    private const string ForeignOwner = "thirdparty.tests.patchshield-foreign";
    private static Harmony _shield = null!;
    private static Harmony _foreign = null!;

    private static readonly string MarkerPrefix = "(rethrown by a Harmony finalizer on ";

    public static class Targets
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static void SwallowVoid() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int SwallowWithResult() => 7;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void RethrowVoid() => throw new InvalidOperationException("034 rethrow");
        [MethodImpl(MethodImplOptions.NoInlining)] public static int RethrowWithResult() => throw new InvalidOperationException("034 rethrow");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void InnerShielded() => throw new InvalidOperationException("034 nested");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void OuterShielded() => InnerShielded();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Probed() => throw new InvalidOperationException("034 probe");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void OuterOfUnknownFrame() => Probe.UnknownFrame!();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void OuterOfUnknownSwallow() => Probe.UnknownSwallow!();
        [MethodImpl(MethodImplOptions.NoInlining)] public static void ProbedThroughPatchedFinalizer() => throw new InvalidOperationException("034 patched finalizer");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void SwallowProtected() { }
    }

    // Install gives a constructor the void finalizer, and Harmony maps a constructor's replacement to its
    // ConstructorInfo, so the rethrow marker must name "<type>..ctor".
    public class ShieldedCtor
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public ShieldedCtor() => throw new InvalidOperationException("034 ctor");
    }

    public static class ForeignPatches
    {
        public static string NextMessage = "";
        public static void ThrowMissingMethod() => throw new MissingMethodException(NextMessage);
        public static void Noop() { }
    }

    // A finalizer another mod has Harmony-patched: its body then runs inside the replacement Harmony built
    // for it, a dynamic method with no declaring type that Harmony maps back to this finalizer. That frame
    // is still the finalizer's own, so the lookup must skip it and judge the shielded method's replacement.
    public static class PatchedProbe
    {
        public static MethodBase? Seen;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Exception? Finalizer(Exception __exception)
        {
            if (__exception != null) Seen = PatchShield.ResolveShieldedOriginal(typeof(PatchedProbe));
            return null;   // swallow: the probe only records
        }
    }

    public static class Probe
    {
        public static MethodBase? Seen;
        public static MethodBase? SeenFromUnknownFrame;
        public static Action? UnknownFrame;

        public static Exception? Finalizer(Exception __exception)
        {
            if (__exception != null) Seen = PatchShield.ResolveShieldedOriginal(typeof(Probe));
            return null;   // swallow: the probe only records
        }

        public static void Postfix() { }

        // A dynamic method Harmony never built. It stands in for a replacement whose frame does not
        // resolve (one another Harmony copy built, say): it calls the lookup directly, so its own frame is
        // the one judged, and the patched OuterOfUnknownFrame's replacement sits one frame further up.
        // It stores the result instead of returning it: a "call; ret" body lets the x64 JIT make an
        // implicit tail call, which removes this frame from the stack and hides the case under test.
        public static Action BuildUnknownFrame()
        {
            var resolve = AccessTools.Method(typeof(PatchShield), nameof(PatchShield.ResolveShieldedOriginal));
            var dynamicMethod = new DynamicMethod("Plan034UnknownFrame", typeof(void), Type.EmptyTypes,
                typeof(PatchShieldFinalizerTests).Module, skipVisibility: true);
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldtoken, typeof(Probe));
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle)));
            il.Emit(OpCodes.Call, resolve);
            il.Emit(OpCodes.Stsfld, AccessTools.Field(typeof(Probe), nameof(SeenFromUnknownFrame)));
            il.Emit(OpCodes.Ret);
            return (Action)dynamicMethod.CreateDelegate(typeof(Action));
        }

        public static Exception? SwallowResult;
        public static Action? UnknownSwallow;

        // The real ShieldFinalizerVoid, handed a MissingMethodException by a dynamic method Harmony never
        // built: the production swallow path when the lookup misses. The patched OuterOfUnknownSwallow,
        // which carries a foreign postfix, sits one frame further up; a lookup that climbed to it would strip
        // that postfix. The result is stored, not returned, for the tail-call reason given above.
        public static Action BuildUnknownSwallow()
        {
            var finalizer = AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid");
            var dynamicMethod = new DynamicMethod("Plan034UnknownSwallow", typeof(void), Type.EmptyTypes,
                typeof(PatchShieldFinalizerTests).Module, skipVisibility: true);
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, AccessTools.Field(typeof(ForeignPatches), nameof(ForeignPatches.NextMessage)));
            il.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(MissingMethodException), new[] { typeof(string) }));
            il.Emit(OpCodes.Call, finalizer);
            il.Emit(OpCodes.Stsfld, AccessTools.Field(typeof(Probe), nameof(SwallowResult)));
            il.Emit(OpCodes.Ret);
            return (Action)dynamicMethod.CreateDelegate(typeof(Action));
        }
    }

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _shield = new Harmony(ShieldOwner);
        _foreign = new Harmony(ForeignOwner);
        var prefix = new HarmonyMethod(AccessTools.Method(typeof(ForeignPatches), nameof(ForeignPatches.ThrowMissingMethod)));
        _foreign.Patch(Target(nameof(Targets.SwallowVoid)), prefix: prefix);
        _foreign.Patch(Target(nameof(Targets.SwallowWithResult)), prefix: prefix);

        Shield(nameof(Targets.SwallowVoid), "ShieldFinalizerVoid");
        Shield(nameof(Targets.SwallowWithResult), "ShieldFinalizerWithResult");
        Shield(nameof(Targets.RethrowVoid), "ShieldFinalizerVoid");
        Shield(nameof(Targets.RethrowWithResult), "ShieldFinalizerWithResult");
        Shield(nameof(Targets.InnerShielded), "ShieldFinalizerVoid");
        Shield(nameof(Targets.OuterShielded), "ShieldFinalizerVoid");

        // A throwing prefix whose owner PatchShield refuses to strip (the shield's own "taom" id is protected),
        // so this resolved method keeps throwing and its swallow line would repeat on every call.
        _shield.Patch(Target(nameof(Targets.SwallowProtected)), prefix: prefix);
        Shield(nameof(Targets.SwallowProtected), "ShieldFinalizerVoid");

        _shield.Patch(Target(nameof(Targets.Probed)),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(Probe), nameof(Probe.Finalizer))));
        _shield.Patch(Target(nameof(Targets.OuterOfUnknownFrame)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(Probe), nameof(Probe.Postfix))));
        Probe.UnknownFrame = Probe.BuildUnknownFrame();

        // A postfix, not a prefix: code after the call keeps the outer replacement's frame on the stack (with
        // only a prefix its body ends "call; ret", and the x64 JIT may make that a tail call).
        _foreign.Patch(Target(nameof(Targets.OuterOfUnknownSwallow)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(ForeignPatches), nameof(ForeignPatches.Noop))));
        Probe.UnknownSwallow = Probe.BuildUnknownSwallow();

        _shield.Patch(AccessTools.Constructor(typeof(ShieldedCtor)),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid")));

        var patchedProbe = AccessTools.Method(typeof(PatchedProbe), nameof(PatchedProbe.Finalizer));
        _shield.Patch(Target(nameof(Targets.ProbedThroughPatchedFinalizer)), finalizer: new HarmonyMethod(patchedProbe));
        _foreign.Patch(patchedProbe, postfix: new HarmonyMethod(AccessTools.Method(typeof(Probe), nameof(Probe.Postfix))));
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        _shield.UnpatchAll(ShieldOwner);
        _foreign.UnpatchAll(ForeignOwner);
    }

    private static MethodInfo Target(string name) => AccessTools.Method(typeof(Targets), name);

    private static void Shield(string target, string finalizer)
    {
        var method = AccessTools.Method(typeof(PatchShield), finalizer);
        Assert.IsNotNull(method, $"PatchShield.{finalizer} did not resolve");
        _shield.Patch(Target(target), finalizer: new HarmonyMethod(method));
    }

    private static long DiagLogLength()
    {
        var path = RuntimeLog.Path;
        Assert.IsFalse(string.IsNullOrEmpty(path), "RuntimeLog.Path did not resolve in the test process");
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static string DiagLogSince(long offset)
    {
        using var stream = new FileStream(RuntimeLog.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Exception Capture(Action call)
    {
        try { call(); }
        catch (Exception ex) { return ex; }
        throw new AssertFailedException("the target did not throw");
    }

    private static string Marker(string target) => MarkerPrefix + typeof(Targets).FullName + "." + target + ") ---";

    private static void AssertSwallowedLoggedAndStripped(string target, string message, long logOffset,
        long swallowedBefore, int unpatchedBefore)
    {
        var method = Target(target);
        var log = DiagLogSince(logOffset);
        Assert.AreEqual(1L, PatchShield.SwallowedMissingMethod - swallowedBefore, "one MissingMethodException swallowed");
        StringAssert.Contains(log, $"swallowed MissingMethodException from a patch on {typeof(Targets).FullName}.{target}: {message}");
        StringAssert.Contains(log, $"unpatched owner '{ForeignOwner}' on {method.Module.ModuleVersionId}:{method.MetadataToken}");
        Assert.AreEqual(1, PatchShield.UnpatchedCount - unpatchedBefore, "the target is recorded as unpatched once");
        var info = Harmony.GetPatchInfo(method);
        Assert.IsFalse(info.Prefixes.Any(p => p.owner == ForeignOwner), "the foreign owner's prefix must be stripped");
        Assert.IsTrue(info.Finalizers.Any(p => p.owner == ShieldOwner), "the shield's own finalizer must stay");
    }

    [TestMethod]
    public void ShieldFinalizerVoid_ForeignPrefixThrowsMissingMethod_SwallowsLogsAndStripsTheOwner()
    {
        var message = "plan034-" + Guid.NewGuid().ToString("N");
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        int unpatchedBefore = PatchShield.UnpatchedCount;

        Targets.SwallowVoid();   // must not throw

        AssertSwallowedLoggedAndStripped(nameof(Targets.SwallowVoid), message, offset, swallowedBefore, unpatchedBefore);
    }

    [TestMethod]
    public void ShieldFinalizerWithResult_ForeignPrefixThrowsMissingMethod_ReturnsDefaultLogsAndStripsTheOwner()
    {
        var message = "plan034-" + Guid.NewGuid().ToString("N");
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        int unpatchedBefore = PatchShield.UnpatchedCount;

        var result = Targets.SwallowWithResult();

        Assert.AreEqual(0, result, "a swallowed call returns default, never the original's 7");
        AssertSwallowedLoggedAndStripped(nameof(Targets.SwallowWithResult), message, offset, swallowedBefore, unpatchedBefore);
    }

    [TestMethod]
    public void ShieldFinalizerVoid_NonTrinityException_MarkerNamesTheShieldedMethod()
    {
        var ex = Capture(Targets.RethrowVoid);

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.RethrowVoid)));
    }

    [TestMethod]
    public void ShieldFinalizerWithResult_NonTrinityException_MarkerNamesTheShieldedMethod()
    {
        var ex = Capture(() => Targets.RethrowWithResult());

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.RethrowWithResult)));
    }

    [TestMethod]
    public void ShieldFinalizer_NestedShieldedMethods_EachMarkerNamesItsOwnMethod()
    {
        // The outer finalizer runs after the inner replacement has unwound, so each must find its own.
        var ex = Capture(Targets.OuterShielded);

        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.InnerShielded)));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.OuterShielded)));
    }

    [TestMethod]
    public void ShieldFinalizers_Parameters_TakeOnlyTheException()
    {
        // Plan 034: a finalizer parameter named __originalMethod makes Harmony's wrapper call
        // MethodBase.GetMethodFromHandle on every call of every shielded method (Step 2's benchmark
        // measured 63 ns per call). The finalizers resolve the shielded method on a throw instead.
        foreach (var name in new[] { "ShieldFinalizerVoid", "ShieldFinalizerWithResult" })
        {
            var method = AccessTools.Method(typeof(PatchShield), name);
            Assert.IsNotNull(method, name);
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, $"{name}({string.Join(", ", parameters.Select(p => p.Name))})");
            Assert.AreEqual("__exception", parameters[0].Name, name);
            Assert.AreEqual(typeof(Exception), parameters[0].ParameterType, name);
        }
    }

    [TestMethod]
    public void ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal()
    {
        // The MonoMod-generated replacement is on the stack; Harmony maps it back to the original.
        Probe.Seen = null;
        long missesBefore = PatchShield.UnresolvedOriginalCount;

        Targets.Probed();   // throws inside the replacement; the probe swallows

        Assert.AreEqual(0L, PatchShield.UnresolvedOriginalCount - missesBefore, "a resolved lookup is not a miss");
        Assert.IsNotNull(Probe.Seen, "the frame that called the probe did not resolve to a Harmony replacement");
        // Declaring type and name first: a returned replacement has no declaring type, and reading a
        // dynamic method's MethodHandle throws, which would hide what went wrong.
        Assert.AreEqual(typeof(Targets), Probe.Seen!.DeclaringType, $"resolved {Probe.Seen.Name}");
        Assert.AreEqual(nameof(Targets.Probed), Probe.Seen.Name);
        Assert.AreEqual(Target(nameof(Targets.Probed)).MethodHandle, Probe.Seen.MethodHandle);
    }

    [TestMethod]
    public void ResolveShieldedOriginal_CallerIsNotAReplacement_ReturnsNull()
    {
        // Called straight from this test method, which Harmony never built: the judged frame is this one.
        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
    }

    [TestMethod]
    public void ResolveShieldedOriginal_JudgedFrameDoesNotResolve_ReturnsNullNotAnOuterPatchedMethod()
    {
        // A lookup that kept climbing past a frame it could not resolve would return the patched
        // OuterOfUnknownFrame further up the stack, and the swallow path would then strip the foreign
        // owners' patches from that innocent method.
        Probe.SeenFromUnknownFrame = null;

        Targets.OuterOfUnknownFrame();

        var seen = Probe.SeenFromUnknownFrame;
        Assert.IsNull(seen, $"resolved {seen?.DeclaringType?.FullName}.{seen?.Name}");
    }

    [TestMethod]
    public void ResolveShieldedOriginal_FinalizerPatchedByAnotherMod_ReturnsTheShieldedMethodNotTheFinalizer()
    {
        // Another owner's postfix on the finalizer runs its body inside that patch's replacement, which
        // Harmony maps back to the finalizer. Naming the finalizer would point the swallow line, the unpatch
        // and the rethrow marker at PatchShield itself and leave the real offender patched.
        PatchedProbe.Seen = null;

        Targets.ProbedThroughPatchedFinalizer();   // throws; the patched probe swallows

        var seen = PatchedProbe.Seen;
        Assert.IsNotNull(seen, "the lookup found nothing");
        Assert.AreEqual(typeof(Targets), seen!.DeclaringType, $"resolved {seen.DeclaringType?.FullName}.{seen.Name}");
        Assert.AreEqual(Target(nameof(Targets.ProbedThroughPatchedFinalizer)).MethodHandle, seen.MethodHandle);
    }

    [TestMethod]
    public void ShieldFinalizerVoid_ShieldedConstructorThrows_MarkerNamesTheConstructor()
    {
        var ex = Capture(() => new ShieldedCtor());

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, MarkerPrefix + typeof(ShieldedCtor).FullName + "..ctor) ---");
    }

    [TestMethod]
    public void ShieldFinalizerVoid_LookupMisses_SwallowsNamesUnknownAndStripsNothing()
    {
        // The production swallow path when the shielded method cannot be named: the exception is still
        // swallowed, the line names ?.?, the miss is counted, and no owner is stripped anywhere, least of all
        // from the patched method one frame further up.
        var message = "plan034-miss-" + Guid.NewGuid().ToString("N");
        ForeignPatches.NextMessage = message;
        Probe.SwallowResult = new Exception("not set");
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        long missesBefore = PatchShield.UnresolvedOriginalCount;
        int unpatchedBefore = PatchShield.UnpatchedCount;

        Targets.OuterOfUnknownSwallow();

        Assert.IsNull(Probe.SwallowResult, "the finalizer must swallow a MissingMethodException");
        Assert.AreEqual(1L, PatchShield.SwallowedMissingMethod - swallowedBefore, "one MissingMethodException swallowed");
        Assert.AreEqual(1L, PatchShield.UnresolvedOriginalCount - missesBefore, "the miss is counted");
        StringAssert.Contains(DiagLogSince(offset), $"swallowed MissingMethodException from a patch on ?.?: {message}");
        Assert.AreEqual(0, PatchShield.UnpatchedCount - unpatchedBefore, "nothing is recorded as unpatched");
        var info = Harmony.GetPatchInfo(Target(nameof(Targets.OuterOfUnknownSwallow)));
        Assert.IsTrue(info.Postfixes.Any(p => p.owner == ForeignOwner), "the outer method's foreign postfix must stay");
    }

    private static int CountOf(string log, string text) => Regex.Matches(log, Regex.Escape(text)).Count;

    // A patch the shield cannot strip keeps throwing, and its swallow line used to be written on every call
    // (maintainer decision D16). The first occurrence of a line is written in full, each repeat is counted,
    // and the session summary reports the total, as the line plus this suffix (D6: aggregate, never drop).
    private static string CountLine(string line, int more) =>
        $"[PatchShield] {line} (and {more} more this session, counted instead of logged)";

    [TestMethod]
    public void ShieldFinalizerVoid_LookupMissesRepeatedly_LogsTheLineOnceThenCountsTheRepeats()
    {
        var message = "plan034-repeat-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        long missesBefore = PatchShield.UnresolvedOriginalCount;

        for (int i = 0; i < 5; i++) Targets.OuterOfUnknownSwallow();   // the same foreign patch throws on every call

        Assert.AreEqual(5L, PatchShield.SwallowedMissingMethod - swallowedBefore, "every call is still swallowed and counted");
        Assert.AreEqual(5L, PatchShield.UnresolvedOriginalCount - missesBefore, "every miss is still counted");
        var log = DiagLogSince(offset);
        Assert.AreEqual(1, CountOf(log, line), "the line is written in full once");
        Assert.IsFalse(log.Contains("counted instead of logged"), "the count waits for a boundary: a mission start or the session summary");

        PatchShield.WriteSessionSummary();

        log = DiagLogSince(offset);
        StringAssert.Contains(log, CountLine(line, 4));
        Assert.AreEqual(2, CountOf(log, line), "the first line and its count line, nothing else");
    }

    [TestMethod]
    public void ShieldFinalizerVoid_LookupMissesWithDifferentMessages_EachLineIsWrittenInFullAndOnlyARepeatIsCounted()
    {
        // Two unknown methods that throw different messages are two different lines. Counting both under ?.?
        // would hide the second mod's missing API (D6).
        var repeated = "plan034-repeat-a-" + Guid.NewGuid().ToString("N");
        var single = "plan034-repeat-b-" + Guid.NewGuid().ToString("N");
        var repeatedLine = $"swallowed MissingMethodException from a patch on ?.?: {repeated}";
        var singleLine = $"swallowed MissingMethodException from a patch on ?.?: {single}";
        long offset = DiagLogLength();

        ForeignPatches.NextMessage = repeated;
        Targets.OuterOfUnknownSwallow();
        Targets.OuterOfUnknownSwallow();
        ForeignPatches.NextMessage = single;
        Targets.OuterOfUnknownSwallow();

        var log = DiagLogSince(offset);
        Assert.AreEqual(1, CountOf(log, repeatedLine), "the repeated line is written in full once");
        Assert.AreEqual(1, CountOf(log, singleLine), "the other message is a different line, written in full");

        PatchShield.WriteSessionSummary();

        log = DiagLogSince(offset);
        StringAssert.Contains(log, CountLine(repeatedLine, 1));
        Assert.AreEqual(1, CountOf(log, singleLine), "a line that never repeated has no count line");
    }

    [TestMethod]
    public void ShieldFinalizerVoid_ProtectedOwnerKeepsThrowingOnAResolvedMethod_LogsTheLineOnceThenCountsTheRepeats()
    {
        // The repeat is not only a lookup miss. PatchShield refuses to strip a protected owner, so a broken
        // patch of one (a BUTR or TAOM id) on a method the lookup did name throws on every call as well.
        var message = "plan034-repeat-" + Guid.NewGuid().ToString("N");
        var target = nameof(Targets.SwallowProtected);
        var line = $"swallowed MissingMethodException from a patch on {typeof(Targets).FullName}.{target}: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;

        for (int i = 0; i < 3; i++) Targets.SwallowProtected();   // must not throw

        Assert.AreEqual(3L, PatchShield.SwallowedMissingMethod - swallowedBefore, "every call is still swallowed and counted");
        var log = DiagLogSince(offset);
        Assert.AreEqual(1, CountOf(log, line), "the line is written in full once");
        StringAssert.Contains(log, $"refusing to unpatch protected owner '{ShieldOwner}'");
        Assert.IsTrue(Harmony.GetPatchInfo(Target(target)).Prefixes.Any(p => p.owner == ShieldOwner),
            "the protected owner's prefix is not stripped, which is why it keeps throwing");

        PatchShield.WriteSessionSummary();

        StringAssert.Contains(DiagLogSince(offset), CountLine(line, 2));
    }

    [TestMethod]
    public void FormatRepeatedSwallow_LineAndCount_MatchesThePinnedLine()
    {
        Assert.AreEqual(
            "swallowed MissingMethodException from a patch on ?.?: m (and 41 more this session, counted instead of logged)",
            PatchShield.FormatRepeatedSwallow("swallowed MissingMethodException from a patch on ?.?: m", 41));
    }

    // The count before the session ends is a running total, so it says so: a session that crashes keeps the last of
    // these (maintainer decision D6: aggregate, never drop).
    private static string CheckpointLine(string line, int more) =>
        $"[PatchShield] {line} (and {more} more so far this session, counted instead of logged)";

    [TestMethod]
    public void FormatRepeatedSwallowSoFar_LineAndCount_MatchesThePinnedLine()
    {
        Assert.AreEqual(
            "swallowed MissingMethodException from a patch on ?.?: m (and 41 more so far this session, counted instead of logged)",
            PatchShield.FormatRepeatedSwallowSoFar("swallowed MissingMethodException from a patch on ?.?: m", 41));
    }

    [TestMethod]
    public void WriteRepeatCheckpoint_LineRepeated_WritesTheRunningCountOnceAndTheSessionSummaryStillGivesTheTotal()
    {
        // A session that crashes never reaches WriteSessionSummary, so the count goes out at a boundary the engine
        // does reach: the start of a mission calls this (see SubModule_MissionStarts_WritesTheRepeatCounts).
        var message = "plan034-checkpoint-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();

        for (int i = 0; i < 3; i++) Targets.OuterOfUnknownSwallow();
        Assert.IsFalse(DiagLogSince(offset).Contains("so far this session"), "no count is written between boundaries");

        PatchShield.WriteRepeatCheckpoint();   // a mission starts

        StringAssert.Contains(DiagLogSince(offset), CheckpointLine(line, 2));
        Assert.AreEqual(1, CountOf(DiagLogSince(offset), line + " (and"), "one count line for the line");

        long afterFirstReport = DiagLogLength();
        PatchShield.WriteRepeatCheckpoint();   // the next mission starts and the line did not recur

        Assert.IsFalse(DiagLogSince(afterFirstReport).Contains(line), "a count that did not change is not written again");

        Targets.OuterOfUnknownSwallow();
        PatchShield.WriteRepeatCheckpoint();   // and one more mission after it recurred

        StringAssert.Contains(DiagLogSince(afterFirstReport), CheckpointLine(line, 3));

        PatchShield.WriteSessionSummary();     // the clean exit

        StringAssert.Contains(DiagLogSince(afterFirstReport), CountLine(line, 3));
        Assert.AreEqual(1, CountOf(DiagLogSince(offset), line + " (and 3 more this session"), "the total is stated at the exit");
    }

    [TestMethod]
    public void WriteRepeatCheckpoint_NoLineRepeated_WritesNothingForAnUnrepeatedLine()
    {
        var message = "plan034-checkpoint-single-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        Targets.OuterOfUnknownSwallow();
        long offset = DiagLogLength();

        PatchShield.WriteRepeatCheckpoint();

        Assert.IsFalse(DiagLogSince(offset).Contains(line), "a line seen once is in the log in full and has no count");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]   // constructs the engine-derived SubModule, whose static constructor loads TaleWorlds assemblies
    public void SubModule_MissionStarts_WritesTheRepeatCounts()
    {
        // MBSubModuleBase gives no mission-end callback; the start of the next mission is the boundary it does give.
        var message = "plan034-mission-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        Targets.OuterOfUnknownSwallow();
        Targets.OuterOfUnknownSwallow();
        long offset = DiagLogLength();

        new DepsSubModule().OnBeforeMissionBehaviorInitialize(null!);

        StringAssert.Contains(DiagLogSince(offset), CheckpointLine(line, 1));
    }

    // Holds diag.log open the way a viewer or a scanner can, so a DiagLog write fails with a sharing violation.
    private static FileStream HoldDiagLogOpen()
    {
        DiagLog.Log("PatchShieldFinalizerTests", "diag.log exists before it is locked");
        return new FileStream(RuntimeLog.Path, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    [TestMethod]
    public void ShieldFinalizerVoid_FirstWriteOfTheLineFails_TheNextOccurrenceWritesItInFullThenCountsTheRest()
    {
        // DiagLog swallows a failed write, so the first occurrence used to look written and every later one was
        // counted: the line could be missing from the log for the whole session.
        var message = "plan034-retry-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;

        using (HoldDiagLogOpen())
        {
            Targets.OuterOfUnknownSwallow();   // swallowed, but diag.log cannot be written
        }
        Assert.AreEqual(0, CountOf(DiagLogSince(offset), line), "the write failed, so the line is not in the log");

        Targets.OuterOfUnknownSwallow();   // diag.log is free again

        Assert.AreEqual(1, CountOf(DiagLogSince(offset), line), "the next occurrence writes the line in full");

        Targets.OuterOfUnknownSwallow();

        Assert.AreEqual(1, CountOf(DiagLogSince(offset), line), "once it landed, a repeat is counted, not written");
        Assert.AreEqual(3L, PatchShield.SwallowedMissingMethod - swallowedBefore, "every call is still swallowed and counted");

        PatchShield.WriteSessionSummary();

        // One occurrence whose write failed and one that came after the line landed.
        StringAssert.Contains(DiagLogSince(offset), CountLine(line, 2));
    }

    [TestMethod]
    public void ShieldFinalizerVoid_OnlyWriteOfTheLineFails_TheSessionSummaryCountLineIsTheOnlyCopyOfItsText()
    {
        // The line happened once and its write did not land, so the limiter counted it: the summary's "and 1 more"
        // line is then everything the log ever holds of it, though the line never recurred.
        var message = "plan034-only-write-failed-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();

        using (HoldDiagLogOpen())
        {
            Targets.OuterOfUnknownSwallow();   // swallowed once, and diag.log cannot be written
        }
        Assert.AreEqual(0, CountOf(DiagLogSince(offset), line), "the write failed, so the line is not in the log");

        PatchShield.WriteSessionSummary();

        var log = DiagLogSince(offset);
        StringAssert.Contains(log, CountLine(line, 1));
        Assert.AreEqual(1, CountOf(log, line), "the count line is the only copy of the text");
    }

    [TestMethod]
    public void WriteRepeatCheckpoint_WriteFails_TheNextCheckpointSendsTheCountAgain()
    {
        // A count that did not reach diag.log is not a report. The crash a checkpoint exists for would otherwise
        // keep only the first line: the next mission start finds nothing new for a line that did not recur.
        var message = "plan034-checkpoint-retry-" + Guid.NewGuid().ToString("N");
        var line = $"swallowed MissingMethodException from a patch on ?.?: {message}";
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        for (int i = 0; i < 3; i++) Targets.OuterOfUnknownSwallow();   // the line in full once, then two counted

        using (HoldDiagLogOpen())
        {
            PatchShield.WriteRepeatCheckpoint();   // a mission starts while diag.log cannot be written
        }
        Assert.AreEqual(0, CountOf(DiagLogSince(offset), CheckpointLine(line, 2)), "the write failed, so the count is not in the log");

        PatchShield.WriteRepeatCheckpoint();   // the next mission starts, and the line did not recur

        Assert.AreEqual(1, CountOf(DiagLogSince(offset), CheckpointLine(line, 2)), "the count that never landed is sent again");

        long afterReport = DiagLogLength();
        PatchShield.WriteRepeatCheckpoint();   // it landed, so a third mission start has nothing new

        Assert.IsFalse(DiagLogSince(afterReport).Contains(line), "once it landed, a count that did not change is not written again");
    }

    [TestMethod]
    public void ResolveShieldedOriginal_FirstMissLineFailsToWrite_TheNextMissLogsItOnce()
    {
        // The reason line is written once per session, and a write that failed must not use that once up.
        AccessTools.Field(typeof(PatchShield), "_unresolvedOriginalLogged").SetValue(null, 0);
        long offset = DiagLogLength();

        using (HoldDiagLogOpen())
        {
            Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
        }
        Assert.AreEqual(0, CountOf(DiagLogSince(offset), "could not tell which shielded method"), "the write failed");

        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
        Assert.AreEqual(1, CountOf(DiagLogSince(offset), "could not tell which shielded method"), "the next miss logs it");

        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
        Assert.AreEqual(1, CountOf(DiagLogSince(offset), "could not tell which shielded method"), "and then it stays once");
    }

    // A lookup that finds nothing degrades the swallow's log line, the unpatch and the rethrow marker, so
    // diag.log says why once, and the session summary counts every miss (maintainer decision D6 in
    // plans/_audit/2026-10-02-perf/DECISIONS.md).
    private const string UnresolvedLine =
        "could not tell which shielded method an exception crossed (the calling frame X.Y is not a replacement " +
        "this Harmony copy maps): the shield still swallows or rethrows it as before, but its log line names '?.?', " +
        "nothing is unpatched for it and its rethrow marker says 'an unknown method'. Logged once per session; " +
        "the session summary counts every miss.";

    [TestMethod]
    public void FormatUnresolvedOriginal_Reason_MatchesThePinnedLine()
    {
        Assert.AreEqual(UnresolvedLine,
            PatchShield.FormatUnresolvedOriginal("the calling frame X.Y is not a replacement this Harmony copy maps"));
    }

    [TestMethod]
    public void ResolveShieldedOriginal_CallerIsNotAReplacement_CountsEveryMissAndLogsTheFirstOnce()
    {
        // The once-flag is process-wide and another test may have tripped it first; clear it so this test
        // sees the first miss.
        AccessTools.Field(typeof(PatchShield), "_unresolvedOriginalLogged").SetValue(null, 0);
        long offset = DiagLogLength();
        long missesBefore = PatchShield.UnresolvedOriginalCount;

        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));

        Assert.AreEqual(2L, PatchShield.UnresolvedOriginalCount - missesBefore, "every miss is counted");
        var expected = PatchShield.FormatUnresolvedOriginal(
            "the calling frame " + typeof(PatchShieldFinalizerTests).FullName + "." +
            nameof(ResolveShieldedOriginal_CallerIsNotAReplacement_CountsEveryMissAndLogsTheFirstOnce) +
            " is not a replacement this Harmony copy maps");
        var log = DiagLogSince(offset);
        Assert.AreEqual(1, Regex.Matches(log, Regex.Escape("could not tell which shielded method")).Count, log);
        StringAssert.Contains(log, "[PatchShield] " + expected);
    }

    [TestMethod]
    public void WriteSessionSummary_NoMiss_ReadsAsBefore()
    {
        // The miss counter is process-wide and other tests miss on purpose: zero it for this run, then put
        // it back so later deltas stay meaningful.
        var counter = AccessTools.Field(typeof(PatchShield), "_unresolvedOriginal");
        var saved = (long)counter.GetValue(null);
        counter.SetValue(null, 0L);
        try
        {
            long offset = DiagLogLength();

            PatchShield.WriteSessionSummary();

            var log = DiagLogSince(offset);
            StringAssert.Contains(log, " with the stack preserved. Top unpatched owner: ");
            Assert.IsFalse(log.Contains("the shielded method was unknown"), log);
        }
        finally
        {
            counter.SetValue(null, saved);
        }
    }

    [TestMethod]
    public void WriteSessionSummary_AfterAMiss_CountsTheUnknownShieldedMethods()
    {
        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
        long misses = PatchShield.UnresolvedOriginalCount;
        long offset = DiagLogLength();

        PatchShield.WriteSessionSummary();

        StringAssert.Contains(DiagLogSince(offset),
            $" with the stack preserved; the shielded method was unknown {misses} time(s). Top unpatched owner: ");
    }
}
