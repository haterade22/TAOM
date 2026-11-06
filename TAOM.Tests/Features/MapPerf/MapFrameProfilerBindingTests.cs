using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.Enlistment;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Features.MissionPerf;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// Drift guard for Patch101. Its transpiler is fail-safe (a missing site leaves <c>CampaignEvents.Tick</c>
/// vanilla with one warning) and its listener walk copies vanilla <c>MbEvent&lt;T&gt;.InvokeList</c>, so an
/// engine bump that moves the call site or changes the loop would silently attribute nothing or change the
/// dispatch; these read the REAL installed IL. The PatchShield walks pin decision D13 (option B, the
/// maintainer, 2026-10-03): the three engine targets only this profiler patches (Campaign.Tick, CampaignEvents.Tick,
/// GameMenuVM.OnFrameTick) stay off PatchShield's per-call finalizer, and the four that other TAOM patches use for every player
/// stay under it.
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
        foreach (var view in new[] { "FieldCampMapView", "MomentumIndicatorMapView" })
            Assert.IsTrue(targets.Any(m => m.DeclaringType?.Name == view && m.Name == "OnMapScreenUpdate"),
                view + ".OnMapScreenUpdate is missing from the targets.");
    }

    // The menu probes (#1019): a Patch101 sub-category, installed only with the toggle on, whose patch classes carry the
    // suffix IsPatchedByThisProfiler looks for and bracket their target (prefix first, postfix last).
    [TestMethod]
    public void MenuProbes_AreASubCategory_WithTheProfilerSuffix_AndTheBracketPriorities()
    {
        var patches = new[]
        {
            typeof(GameMenuVM_OnFrameTick_MapProfiler_Patch), typeof(GameMenuVM_ContextText_MapProfiler_Patch),
            typeof(ServiceStatusService_RefreshIfChanged_MapProfiler_Patch),
        };

        Assert.AreEqual("Patch101_MapFrameProfiler_Menu", MapFrameProfilerInstaller.MenuCategory);
        foreach (var patch in patches)
        {
            Assert.IsTrue(patch.Name.EndsWith("_MapProfiler_Patch", StringComparison.Ordinal), patch.Name);
            Assert.AreEqual(MapFrameProfilerInstaller.MenuCategory, patch.GetCustomAttribute<HarmonyPatchCategory>()!.info.category, patch.Name);
            Assert.AreEqual(Priority.First, patch.GetMethod("Prefix")!.GetCustomAttribute<HarmonyPriority>()!.info.priority, patch.Name);
            Assert.AreEqual(Priority.Last, patch.GetMethod("Postfix")!.GetCustomAttribute<HarmonyPriority>()!.info.priority, patch.Name);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MenuProbes_EachPatchResolvesItsOwnMethod()
    {
        RequireGame();
        var expected = new[]
        {
            (typeof(GameMenuVM_OnFrameTick_MapProfiler_Patch), "OnFrameTick"),
            (typeof(GameMenuVM_ContextText_MapProfiler_Patch), "set_ContextText"),
            (typeof(ServiceStatusService_RefreshIfChanged_MapProfiler_Patch), "RefreshIfChanged"),
        };

        foreach (var (patch, name) in expected)
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.AreEqual(name, target.Name, patch.Name);
        }
    }

    // Campaign.Tick, CampaignEvents.Tick and GameMenuVM.OnFrameTick carry no TAOM patch but Patch101, so excluding them changes nothing for
    // a player with the profiler off as far as TAOM's own patches go; another mod's patch on one of them loses the shield either way.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProfilerOnlyTargets_AreOnPatchShieldsExclusionList()
    {
        RequireGame();
        foreach (var patch in new[] { typeof(Campaign_Tick_MapProfiler_Patch), typeof(CampaignEvents_Tick_MapProfiler_Patch), typeof(GameMenuVM_OnFrameTick_MapProfiler_Patch) })
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
        }
    }

    // The reverse of the walk above: "carries no TAOM patch but Patch101" is the reason the exclusions change nothing for a
    // player with the profiler off, so a second TAOM patch class on one of the three targets would break it silently.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProfilerOnlyTargets_AreSharedWithNoOtherTaomPatchClass()
    {
        RequireGame();
        var profilerOnly = new[] { typeof(Campaign_Tick_MapProfiler_Patch), typeof(CampaignEvents_Tick_MapProfiler_Patch), typeof(GameMenuVM_OnFrameTick_MapProfiler_Patch) }
            .Select(p => Key(TargetOf(p)))
            .ToHashSet();
        Assert.AreEqual(3, profilerOnly.Count);

        // AccessTools.GetTypesFromAssembly is the enumeration Harmony's PatchCategory uses.
        var assemblies = new[] { typeof(MapFrameProfilerInstaller).Assembly };
        var offenders = new List<string>();
        foreach (var type in assemblies.SelectMany(AccessTools.GetTypesFromAssembly))
        {
            if (!type.GetCustomAttributes<HarmonyPatch>().Any() || type.Name.EndsWith("_MapProfiler_Patch", StringComparison.Ordinal))
                continue;
            MethodBase target;
            try { target = TargetOf(type); }
            catch (Exception) { continue; }
            if (target != null && profilerOnly.Contains(Key(target)))
                offenders.Add(type.FullName);
        }

        Assert.AreEqual(0, offenders.Count, "Another TAOM patch class targets a profiler-only method: " + string.Join(", ", offenders));
    }

    private static string Key(MethodBase method) => method.DeclaringType?.FullName + "." + method.Name;

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
                     typeof(GameMenuVM_ContextText_MapProfiler_Patch),
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

    // MissionTickProfilerBindingTests.TargetOf.
    private static MethodBase TargetOf(Type patch)
    {
        var byName = AccessTools.Method(patch, "TargetMethod");
        if (byName != null)
            return (MethodBase)byName.Invoke(null, null);
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }
}
