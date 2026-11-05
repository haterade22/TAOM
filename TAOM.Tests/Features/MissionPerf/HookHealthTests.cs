using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The tick profiler's hook-health rule: a hook counts only while Harmony reports its own patch method, of the
/// right kind, on its target. Driven through real Harmony on test targets (the apply is the one TAOM's category
/// index uses, the removals are the calls PatchShield makes) and through hand-built <see cref="Patches"/> for the
/// shapes Harmony cannot be asked to produce. Touches no engine type.
/// </summary>
[TestClass]
public class HookHealthTests
{
    private const string HarmonyId = "taom.tests.hookhealth";
    private const string ForeignId = "taom.tests.hookhealth.foreign";

    public static class Target
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Tick(float dt) => 1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int PreTick(float dt) => 2;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Agent(float dt, bool paused) { }
    }

    [HarmonyPatch(typeof(Target), nameof(Target.Tick), new[] { typeof(float) })]
    public static class TickPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => instructions;
    }

    [HarmonyPatch(typeof(Target), nameof(Target.PreTick), new[] { typeof(float) })]
    public static class PreTickPatch
    {
        [HarmonyPrefix]
        public static void Prefix() { }
    }

    [HarmonyPatch(typeof(Target), nameof(Target.Agent), new[] { typeof(float), typeof(bool) })]
    public static class AgentPatch
    {
        [HarmonyPrefix]
        public static void Prefix() { }

        [HarmonyFinalizer]
        public static void Finalizer() { }
    }

    /// <summary>Another mod's patch method: no <c>[HarmonyPatch]</c>, applied by hand.</summary>
    public static class ForeignPatch
    {
        public static void Prefix() { }
    }

    public class PatchBase
    {
        public static void Prefix() { }
    }

    public class PatchDerived : PatchBase
    {
    }

    private static readonly RequiredHook TickTranspiler =
        RequiredHook.Of("tick transpiler", typeof(TickPatch), nameof(TickPatch.Transpiler), HarmonyPatchType.Transpiler);
    private static readonly RequiredHook PreTickPrefix =
        RequiredHook.Of("pre-tick prefix", typeof(PreTickPatch), nameof(PreTickPatch.Prefix), HarmonyPatchType.Prefix);
    private static readonly RequiredHook AgentPrefix =
        RequiredHook.Of("agent prefix", typeof(AgentPatch), nameof(AgentPatch.Prefix), HarmonyPatchType.Prefix);
    private static readonly RequiredHook AgentFinalizer =
        RequiredHook.Of("agent finalizer", typeof(AgentPatch), nameof(AgentPatch.Finalizer), HarmonyPatchType.Finalizer);
    private static readonly RequiredHook[] All = { TickTranspiler, PreTickPrefix, AgentPrefix, AgentFinalizer };

    private static readonly MethodInfo TickMethod = AccessTools.Method(typeof(Target), nameof(Target.Tick));
    private static readonly MethodInfo PreTickMethod = AccessTools.Method(typeof(Target), nameof(Target.PreTick));
    private static readonly MethodInfo AgentMethod = AccessTools.Method(typeof(Target), nameof(Target.Agent));

    private Harmony _harmony = null!;
    private Harmony _foreign = null!;

    [TestInitialize]
    public void Setup()
    {
        _harmony = new Harmony(HarmonyId);
        _foreign = new Harmony(ForeignId);
        foreach (var patch in new[] { typeof(TickPatch), typeof(PreTickPatch), typeof(AgentPatch) })
            _harmony.CreateClassProcessor(patch).Patch();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony.UnpatchAll(HarmonyId);
        _foreign.UnpatchAll(ForeignId);
    }

    private static IReadOnlyList<string> Missing() => HookHealth.Missing(All, Harmony.GetPatchInfo);

    [TestMethod]
    public void Of_ReadsTheTargetAndThePatchMethodFromThePatchClass()
    {
        Assert.AreEqual(TickMethod, TickTranspiler.Target);
        Assert.AreEqual(AccessTools.Method(typeof(TickPatch), nameof(TickPatch.Transpiler)), TickTranspiler.PatchMethod);
        Assert.AreEqual(AgentMethod, AgentFinalizer.Target, "The overload with two parameters resolves, not just the name.");
    }

    [TestMethod]
    public void Of_APatchClassWithNoHarmonyPatchAttribute_HasNoTarget()
        => Assert.IsNull(RequiredHook.Of("x", typeof(ForeignPatch), nameof(ForeignPatch.Prefix), HarmonyPatchType.Prefix).Target);

    [TestMethod]
    public void Of_AnUnknownPatchMethodName_HasNoPatchMethod()
        => Assert.IsNull(RequiredHook.Of("x", typeof(TickPatch), "NoSuchMethod", HarmonyPatchType.Prefix).PatchMethod);

    [TestMethod]
    public void Missing_EveryHookApplied_ReturnsNone()
        => Assert.AreEqual(0, Missing().Count);

    [TestMethod]
    public void Missing_NothingApplied_NamesEveryHookInListOrder()
    {
        _harmony.UnpatchAll(HarmonyId);

        CollectionAssert.AreEqual(All.Select(h => h.Name).ToArray(), Missing().ToArray());
    }

    // PatchShield's rescue removes an owner's prefixes, postfixes and transpilers from the method, by type.
    [TestMethod]
    public void Missing_ThePatchShieldStripOfTheTranspilerOnTick_NamesOnlyTheTranspiler()
    {
        _harmony.Unpatch(TickMethod, HarmonyPatchType.Transpiler, HarmonyId);

        CollectionAssert.AreEqual(new[] { "tick transpiler" }, Missing().ToArray());
    }

    [TestMethod]
    public void Missing_ThePatchShieldStripOfThePrefixOnPreTick_NamesOnlyThatPrefix()
    {
        _harmony.Unpatch(PreTickMethod, HarmonyPatchType.Prefix, HarmonyId);

        CollectionAssert.AreEqual(new[] { "pre-tick prefix" }, Missing().ToArray());
    }

    // The strip loop never touches finalizers, so the agent tick keeps its finalizer and loses only its prefix.
    [TestMethod]
    public void Missing_ThePatchShieldStripOnTheAgentTick_NamesTheAgentPrefixAndNotItsFinalizer()
    {
        _harmony.Unpatch(AgentMethod, HarmonyPatchType.Prefix, HarmonyId);

        CollectionAssert.AreEqual(new[] { "agent prefix" }, Missing().ToArray());
    }

    [TestMethod]
    public void Missing_TheFinalizerUnpatchedByMethod_NamesOnlyTheFinalizer()
    {
        _harmony.Unpatch(AgentMethod, AccessTools.Method(typeof(AgentPatch), nameof(AgentPatch.Finalizer)));

        CollectionAssert.AreEqual(new[] { "agent finalizer" }, Missing().ToArray());
    }

    [TestMethod]
    public void Missing_AnotherOwnersPrefixOnTheSameTarget_DoesNotStandInForTheHook()
    {
        _foreign.Patch(PreTickMethod, prefix: new HarmonyMethod(typeof(ForeignPatch).GetMethod(nameof(ForeignPatch.Prefix))));
        _harmony.Unpatch(PreTickMethod, HarmonyPatchType.Prefix, HarmonyId);

        CollectionAssert.AreEqual(new[] { "pre-tick prefix" }, Missing().ToArray());
    }

    [TestMethod]
    public void Missing_PatchInfoIsNull_NamesEveryHook()
        => CollectionAssert.AreEqual(All.Select(h => h.Name).ToArray(), HookHealth.Missing(All, _ => null).ToArray());

    [TestMethod]
    public void Missing_ThePatchMethodIsListedUnderAnotherKind_IsMissing()
    {
        var misfiled = PatchesWith((HarmonyPatchType.Postfix, TickTranspiler.PatchMethod!));

        CollectionAssert.AreEqual(new[] { "tick transpiler" },
            HookHealth.Missing(new[] { TickTranspiler }, _ => misfiled).ToArray());
    }

    [TestMethod]
    public void Missing_ThePatchMethodIsListedUnderItsKind_IsInPlace()
    {
        var inPlace = PatchesWith((HarmonyPatchType.Transpiler, TickTranspiler.PatchMethod!));

        Assert.AreEqual(0, HookHealth.Missing(new[] { TickTranspiler }, _ => inPlace).Count);
    }

    [TestMethod]
    public void Missing_AnUnresolvedTargetOrPatchMethod_IsNamedAsNotResolved()
    {
        var noTarget = new RequiredHook("ghost target", null, HarmonyPatchType.Prefix, PreTickPrefix.PatchMethod);
        var noMethod = new RequiredHook("ghost method", PreTickMethod, HarmonyPatchType.Prefix, null);

        CollectionAssert.AreEqual(new[] { "ghost target (not resolved)", "ghost method (not resolved)" },
            HookHealth.Missing(new[] { noTarget, noMethod }, Harmony.GetPatchInfo).ToArray());
    }

    // The type alone says nothing about why the read failed, so the line carries the message too.
    [TestMethod]
    public void Missing_PatchInfoThrows_NamesTheHookWithTheExceptionTypeAndMessageAndDoesNotThrow()
    {
        var missing = HookHealth.Missing(new[] { TickTranspiler }, _ => throw new InvalidOperationException("harmony broke"));

        CollectionAssert.AreEqual(new[] { "tick transpiler (patch info unreadable: InvalidOperationException: harmony broke)" }, missing.ToArray());
    }

    [TestMethod]
    public void Missing_SeveralHooksOnOneTarget_ReadsPatchInfoOncePerTarget()
    {
        var reads = new List<MethodBase>();

        HookHealth.Missing(All, target => { reads.Add(target); return Harmony.GetPatchInfo(target); });

        CollectionAssert.AreEquivalent(new MethodBase[] { TickMethod, PreTickMethod, AgentMethod }, reads);
    }

    // Harmony hands back freshly deserialized patches, and resolves each one's method by module GUID and token the
    // first time it is read: another reflection object for the same method.
    [TestMethod]
    public void Missing_ThePatchMethodSeenThroughAnotherReflectedType_IsStillTheHook()
    {
        var declared = typeof(PatchBase).GetMethod(nameof(PatchBase.Prefix))!;
        var inherited = typeof(PatchDerived).GetMethod(nameof(PatchBase.Prefix),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
        Assert.IsFalse(declared == inherited, "The premise: two reflection objects for one method.");
        var hook = new RequiredHook("base prefix", PreTickMethod, HarmonyPatchType.Prefix, declared);
        var seen = PatchesWith((HarmonyPatchType.Prefix, inherited));

        Assert.AreEqual(0, HookHealth.Missing(new[] { hook }, _ => seen).Count);
    }

    // A patch from a module that is gone or dynamic resolves to an exception, not a method: it is not the hook,
    // and it must not hide ours.
    [TestMethod]
    public void Missing_APatchWhoseMethodCannotBeResolved_IsSkippedAndDoesNotHideOurs()
    {
        var ours = new Patch(TickTranspiler.PatchMethod!, 1, "com.taom.mod", 0, Array.Empty<string>(), Array.Empty<string>(), false);
        var patches = new Patches(Array.Empty<Patch>(), Array.Empty<Patch>(), new[] { Unresolvable(), ours },
            Array.Empty<Patch>(), Array.Empty<Patch>(), Array.Empty<Patch>());

        Assert.AreEqual(0, HookHealth.Missing(new[] { TickTranspiler }, _ => patches).Count);
    }

    [TestMethod]
    public void Missing_OnlyAPatchWhoseMethodCannotBeResolved_ReadsAsMissingAndDoesNotThrow()
    {
        var patches = new Patches(Array.Empty<Patch>(), Array.Empty<Patch>(), new[] { Unresolvable() },
            Array.Empty<Patch>(), Array.Empty<Patch>(), Array.Empty<Patch>());

        CollectionAssert.AreEqual(new[] { "tick transpiler" }, HookHealth.Missing(new[] { TickTranspiler }, _ => patches).ToArray());
    }

    private static Patch Unresolvable()
    {
        var patch = new Patch(PreTickMethod, 0, "foreign", 0, Array.Empty<string>(), Array.Empty<string>(), false);
        typeof(Patch).GetField("patchMethod", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(patch, null);
        typeof(Patch).GetField("moduleGUID", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(patch, Guid.NewGuid().ToString());
        Assert.ThrowsException<InvalidOperationException>(() => patch.PatchMethod, "The premise: this patch cannot resolve its method.");
        return patch;
    }

    private static Patches PatchesWith(params (HarmonyPatchType Kind, MethodInfo Method)[] entries)
    {
        Patch[] Of(HarmonyPatchType kind) => entries.Where(e => e.Kind == kind)
            .Select((e, i) => new Patch(e.Method, i, "someone", 0, Array.Empty<string>(), Array.Empty<string>(), false))
            .ToArray();
        return new Patches(Of(HarmonyPatchType.Prefix), Of(HarmonyPatchType.Postfix), Of(HarmonyPatchType.Transpiler),
            Of(HarmonyPatchType.Finalizer), Array.Empty<Patch>(), Array.Empty<Patch>());
    }
}
