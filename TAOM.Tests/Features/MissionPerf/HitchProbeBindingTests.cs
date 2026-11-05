using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Drift guard for Patch98 and the two attribution transpilers. Every probe target resolves in the installed
/// engine and is not overridable (a bracket on a base virtual never runs for an override; the camera target
/// is a sealed explicit implementation), and the PatchShield split of maintainer decision D13 (2026-10-03)
/// holds: WaitTickCompletion and TickComponents stay on PatchShield's hot-method exclusion list, while
/// OnPreTick, OnTick and SpawnAgent are off it and the shield wraps them again. UpdateCamera was never on
/// the list; the maintainer's decision of 2026-10-15 (#809) shields it too.
/// </summary>
[TestClass]
public class HitchProbeBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => MissionAttributionInstaller.ResetForTests();

    private static IEnumerable<Type> ProbeClasses() =>
        AccessTools.GetTypesFromAssembly(typeof(HitchProbeHooks).Assembly)
            .Where(t => t.GetCustomAttributes<HarmonyPatchCategory>().Any(c =>
                c.info.category is HitchProbeInstaller.Category or HitchProbeInstaller.CameraCategory));

    // CreatureBanditsWiringTests.TargetOf, with the argument types.
    private static MethodBase TargetOf(Type patch)
    {
        var byName = AccessTools.Method(patch, "TargetMethod");
        if (byName != null)
            return (MethodBase)byName.Invoke(null, null);
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProbeTargets_ResolveInInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var classes = ProbeClasses().ToList();
        Assert.AreEqual(6, classes.Count,
            "The Patch98_HitchProbe category holds five bracket classes, and the camera category one more.");
        foreach (var patch in classes)
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.IsFalse(target.IsVirtual && !target.IsFinal, $"{patch.Name}: {target.DeclaringType?.Name}.{target.Name} is virtual and overridable.");
        }
    }

    // D13: since plan 034 the shield's finalizer is cheap (the figures are in PatchShieldPolicy's ExcludedTargetNamespacePrefixes comment), so the
    // entries kept only for cost went. These two stay.
    private static readonly string[] StillExcluded = { "WaitTickCompletion", "TickComponents" };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProbeAndAttributionTargets_OnlyTheWaitAndTheScriptTickAreOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        foreach (var patch in ProbeClasses().Concat(new[]
                 {
                     typeof(Mission_SpawnAgent_Attribution_Patch), typeof(ManagedScriptHolder_TickComponents_Attribution_Patch),
                 }))
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            var kept = StillExcluded.Contains(target.Name);
            Assert.AreEqual(kept, PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} " + (kept ? "must stay in" : "must not be in")
                + " PatchShieldPolicy.ExcludedTargetMethods");
            if (!kept)
                Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(target.DeclaringType?.Namespace),
                    $"{target.DeclaringType?.FullName}'s namespace must not be on the namespace exclusion list: the shield wraps {target.Name}.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void SpawnAgentRewrite_FindsBothAgentBuildCalls_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = AccessTools.Method(typeof(Mission), nameof(Mission.SpawnAgent));
        Assert.IsNotNull(target, "Mission.SpawnAgent did not resolve.");
        var logger = Substitute.For<IModLogger>();

        var rewritten = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList(),
            MissionAttributionInstaller.SpawnAgentSwaps(), "Mission.SpawnAgent", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, swapped);
        Assert.AreEqual(2, rewritten.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionAttributionHooks)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TickComponentsRewrite_FindsFourParallelForsAndOneOnTick_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(MissionAttributionInstaller.BindScriptTickDelegate(), "ScriptComponentBehavior.OnTick did not bind to an open delegate.");
        var target = AccessTools.Method(typeof(ManagedScriptHolder), "TickComponents", new[] { typeof(float) });
        Assert.IsNotNull(target, "ManagedScriptHolder.TickComponents(float) did not resolve.");
        var logger = Substitute.For<IModLogger>();

        var rewritten = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList(),
            MissionAttributionInstaller.TickComponentsSwaps(), "ManagedScriptHolder.TickComponents", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(5, swapped);
        var blocks = rewritten.Where(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionAttributionHooks)
                                           && m.Name.EndsWith("Block")).Select(ci => ((MethodInfo)ci.operand).Name).ToList();
        CollectionAssert.AreEqual(new[]
        {
            nameof(MissionAttributionHooks.TimedParallelBlock), nameof(MissionAttributionHooks.TimedParallelBlock),
            nameof(MissionAttributionHooks.TimedParallelBlock), nameof(MissionAttributionHooks.TimedOccasionalBlock),
        }, blocks);
        Assert.AreEqual(1, rewritten.Count(ci => ci.operand is MethodInfo m && m.Name == nameof(MissionAttributionHooks.TimedScriptTick)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CameraTarget_IsMissionScreensSealedExplicitUpdateCamera()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = TargetOf(typeof(MissionScreen_UpdateCamera_HitchProbe_Patch));
        Assert.IsNotNull(target, "The camera target did not resolve.");
        Assert.AreEqual("MissionScreen", target.DeclaringType?.Name);
        var map = target.DeclaringType!.GetInterfaceMap(typeof(IMissionSystemHandler));
        var slot = Array.IndexOf(map.InterfaceMethods, typeof(IMissionSystemHandler).GetMethod("UpdateCamera"));
        Assert.IsTrue(slot >= 0, "IMissionSystemHandler.UpdateCamera is not on the interface.");
        Assert.AreEqual(map.TargetMethods[slot], target, "The patch must hit the method MissionScreen maps the interface slot to.");
        Assert.IsTrue(target.IsFinal && target.IsPrivate, "An explicit implementation is private final virtual.");
        CollectionAssert.AreEqual(new[] { typeof(Mission), typeof(float) }, target.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MissionOnTick_CallsHandlerUpdateCamera_Once_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var onTick = AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });
        Assert.IsNotNull(onTick, "Mission.OnTick did not resolve.");
        var handlerCall = typeof(IMissionSystemHandler).GetMethod("UpdateCamera");

        var calls = PatchProcessor.GetOriginalInstructions(onTick).Count(ci => ci.operand is MethodInfo m && m == handlerCall);

        Assert.AreEqual(1, calls, "Mission.OnTick must call IMissionSystemHandler.UpdateCamera exactly once.");
    }
}
