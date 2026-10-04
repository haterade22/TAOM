using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Features.MissionPerf;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// Drift guard for Patch101. Its transpiler is fail-safe (a missing site leaves <c>CampaignEvents.Tick</c>
/// vanilla with one warning) and its listener walk copies vanilla <c>MbEvent&lt;T&gt;.InvokeList</c>, so an
/// engine bump that moves the call site or changes the loop would silently attribute nothing or change the
/// dispatch; these read the REAL installed IL. The two PatchShield walks pin decision D13 (option B, the
/// maintainer, 2026-10-03): the two engine targets only this profiler patches stay off PatchShield's per-call
/// finalizer, and the three that other TAOM patches use for every player stay under it.
/// </summary>
[TestClass]
public class MapFrameProfilerBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CampaignEventsTickRewrite_FindsTheOneInvoke_InInstalledEngine()
    {
        RequireGame();
        var target = AccessTools.Method(typeof(CampaignEvents), nameof(CampaignEvents.Tick), new[] { typeof(float) });
        Assert.IsNotNull(target, "CampaignEvents.Tick(float) did not resolve.");

        var logger = Substitute.For<IModLogger>();
        var rewritten = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList(),
            MapProfilerTargets.TickEventSwaps(), "CampaignEvents.Tick", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, swapped);
        Assert.AreEqual(1, rewritten.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MapFrameProfilerHooks)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MbEventInvokeList_ReadsNextAfterTheCall_AndHasNoHandler()
    {
        RequireGame();
        var invokeList = AccessTools.Method(typeof(MbEvent<float>), "InvokeList");
        Assert.IsNotNull(invokeList, "MbEvent<float>.InvokeList did not resolve.");

        Assert.AreEqual(0, invokeList.GetMethodBody()!.ExceptionHandlingClauses.Count, "InvokeList gained an exception handler.");
        var il = PatchProcessor.GetOriginalInstructions(invokeList).ToList();
        var calls = il.Select((ci, i) => (ci, i)).Where(x => x.ci.opcode == OpCodes.Callvirt && x.ci.operand is MethodInfo m
            && m.Name == "Invoke" && m.DeclaringType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == typeof(Action<>)).ToList();
        var nextReads = il.Select((ci, i) => (ci, i)).Where(x => x.ci.opcode == OpCodes.Ldfld && x.ci.operand is FieldInfo f
            && f.Name == "Next").ToList();

        Assert.AreEqual(1, calls.Count, "InvokeList must call the listener's action exactly once.");
        Assert.AreEqual(1, nextReads.Count, "InvokeList must read Next exactly once.");
        Assert.IsTrue(nextReads[0].i > calls[0].i, "InvokeList must read Next AFTER the call, as the walk does.");
    }

    // The walk replaces all of Invoke, not only InvokeList: a second list, a guard or a handler added to Invoke
    // would pass every other gate while the measuring dispatch silently skipped or changed listeners.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MbEventInvoke_OnlyCallsInvokeListOnTheNonSerializedList_AndHasNoHandler()
    {
        RequireGame();
        var invoke = AccessTools.Method(typeof(MbEvent<float>), nameof(MbEvent<float>.Invoke), new[] { typeof(float) });
        Assert.IsNotNull(invoke, "MbEvent<float>.Invoke(float) did not resolve.");

        Assert.AreEqual(0, invoke.GetMethodBody()!.ExceptionHandlingClauses.Count, "Invoke gained an exception handler.");
        var il = PatchProcessor.GetOriginalInstructions(invoke).ToList();
        var fieldReads = il.Where(ci => ci.operand is FieldInfo).Select(ci => ((FieldInfo)ci.operand).Name).ToList();
        var calls = il.Where(ci => ci.operand is MethodBase).Select(ci => ((MethodBase)ci.operand).Name).ToList();
        var branches = il.Count(ci => ci.operand is Label || ci.operand is Label[]);

        CollectionAssert.AreEqual(new[] { "_nonSerializedListenerList" }, fieldReads, "Invoke must read only the non-serialized list.");
        CollectionAssert.AreEqual(new[] { "InvokeList" }, calls, "Invoke must only call InvokeList.");
        Assert.AreEqual(0, branches, "Invoke gained a branch (a guard).");
    }

    // A member the installed engine lacks, referenced from a hook body, throws when the CLR compiles that body,
    // before its own try is entered, so the boundary's catch cannot contain it (harmony-il.md). Compiling every
    // Patch101 method against the installed engine pins every engine reference they hold.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Patch101Bodies_CompileAgainstInstalledEngine()
    {
        RequireGame();
        const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;
        var types = typeof(MapFrameProfilerHooks).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(MapFrameProfilerHooks).Namespace && !t.ContainsGenericParameters).ToList();
        var compiled = 0;
        var failures = new System.Collections.Generic.List<string>();
        foreach (var type in types)
            foreach (var method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
            {
                if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                    continue;
                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    compiled++;
                }
                catch (Exception ex)
                {
                    failures.Add(type.Name + "." + method.Name + ": " + ex.GetType().Name + ": " + ex.Message);
                }
            }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
        Assert.IsTrue(types.Any(t => t == typeof(MapSessionHooks)) && compiled > 20, "Compiled only " + compiled + " methods.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryCoreTarget_ResolvesInInstalledEngine()
    {
        RequireGame();
        var targets = MapProfilerTargets.CoreTargets();

        Assert.AreEqual(6, targets.Count);
        Assert.IsTrue(targets.All(t => t != null), "Unresolved: " + string.Join(", ",
            MapProfilerTargets.CoreTargetNames.Where((_, i) => targets[i] == null)));
        CollectionAssert.AreEqual(
            new[] { "MapState.OnTick", "Campaign.RealTick", "Campaign.Tick", "CampaignEvents.Tick", "MapScreen.OnFrameTick", "SubModule.OnApplicationTick" },
            targets.Select(t => t!.DeclaringType!.Name + "." + t.Name).ToArray());
        CollectionAssert.AreEqual(targets.Select(t => t!.DeclaringType!.Name + "." + t.Name).ToArray(),
            MapProfilerTargets.CoreTargetNames.ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MapViewTargets_AreTheTaomMapViewPerFrameOverrides()
    {
        RequireGame();
        var targets = MapProfilerTargets.MapViewTargets();
        var perFrame = new[] { "OnFrameTick", "OnMapScreenUpdate", "OnMenuModeTick", "OnIdleTick" };

        foreach (var m in targets)
        {
            var owner = m.DeclaringType!;
            Assert.AreEqual(typeof(MapFrameProfilerHooks).Assembly, owner.Assembly, m.Name + " is not declared on a TAOM type.");
            Assert.IsFalse(owner.IsAbstract, owner.Name);
            Assert.IsTrue(DerivesFromMapView(owner), owner.Name + " does not derive from SandBox.View.Map.MapView.");
            CollectionAssert.Contains(perFrame, m.Name);
            CollectionAssert.AreEqual(new[] { typeof(float) }, m.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        // By name: the test project references TaleWorlds assemblies only, not SandBox.View.
        foreach (var view in new[] { "FieldCampMapView", "MomentumIndicatorMapView", "RealmBordersMapView" })
            Assert.IsTrue(targets.Any(m => m.DeclaringType?.Name == view && m.Name == "OnMapScreenUpdate"),
                view + ".OnMapScreenUpdate is missing from the targets.");
    }

    // Campaign.Tick and CampaignEvents.Tick carry no TAOM patch but Patch101, so excluding them changes nothing for
    // a player with the profiler off.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProfilerOnlyTargets_AreOnPatchShieldsExclusionList()
    {
        RequireGame();
        foreach (var patch in new[] { typeof(Campaign_Tick_MapProfiler_Patch), typeof(CampaignEvents_Tick_MapProfiler_Patch) })
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
        }
    }

    // Decision D13 (option B, the maintainer, 2026-10-03): MapState.OnTick (Patch43), Campaign.RealTick (Patch89)
    // and MapScreen.OnFrameTick (Patch36, Patch89) are patched for every player, so they keep PatchShield's
    // swallow and strip. Listing one again changes crash handling for every player, profiler on or off.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SharedMapTargets_StayUnderPatchShield()
    {
        RequireGame();
        foreach (var patch in new[]
                 {
                     typeof(MapState_OnTick_MapProfiler_Patch), typeof(Campaign_RealTick_MapProfiler_Patch),
                     typeof(MapScreen_OnFrameTick_MapProfiler_Patch),
                 })
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must stay under PatchShield (decision D13, option B)");
            // PatchShield skips a target on its namespace as well as on its method (PatchShield.IsExcludedTarget): a
            // prefix such as "SandBox.View" in ExcludedTargetNamespacePrefixes would unshield MapScreen.OnFrameTick.
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(target.DeclaringType?.Namespace),
                $"{target.DeclaringType?.Namespace} must stay off ExcludedTargetNamespacePrefixes: {target.DeclaringType?.Name}.{target.Name} stays under PatchShield (decision D13, option B)");
        }
    }

    private static bool DerivesFromMapView(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
            if (t.FullName == "SandBox.View.Map.MapView")
                return true;
        return false;
    }

    // MissionTickProfilerBindingTests.TargetOf (CreatureBanditsWiringTests.TargetOf).
    private static MethodBase TargetOf(Type patch)
    {
        var byName = AccessTools.Method(patch, "TargetMethod");
        if (byName != null)
            return (MethodBase)byName.Invoke(null, null);
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }
}
