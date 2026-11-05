using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.BattleLoadDiagnostics.Hooks;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Drift guard for Patch97. Its transpilers are fail-safe (a missing site leaves the method vanilla with
/// one warning), so an engine bump that moves a call site would silently profile nothing; these feed the
/// REAL installed IL through the production rewrite and the real helpers. The hook-health list is walked to its
/// real targets and patch methods. The PatchShield walks pin the agent tick on the exclusion list and the two
/// tick methods off it, by method and by namespace.
/// </summary>
[TestClass]
public class MissionTickProfilerBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => MissionTickProfilerHooks.WaitTickCompletionCall = null;

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnTickRewrite_FindsBothBehaviourCalls_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = AccessTools.Method(typeof(Mission), nameof(Mission.OnTick),
            new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });
        Assert.IsNotNull(target, "Mission.OnTick(float, float, bool, bool) did not resolve.");

        var logger = Substitute.For<IModLogger>();
        var rewritten = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList(),
            MissionTickProfilerInstaller.OnTickSwaps(), "Mission.OnTick", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, swapped);
        Assert.AreEqual(2, rewritten.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionTickProfilerHooks)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnPreTickRewrite_FindsWaitAndPreMissionTick_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(MissionTickProfilerInstaller.BindWaitDelegate(), "Mission.WaitTickCompletion() did not bind to an open delegate.");
        var target = AccessTools.Method(typeof(Mission), "OnPreTick", new[] { typeof(float) });
        Assert.IsNotNull(target, "Mission.OnPreTick(float) did not resolve.");

        var logger = Substitute.For<IModLogger>();
        var rewritten = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(target).ToList(),
            MissionTickProfilerInstaller.OnPreTickSwaps(), "Mission.OnPreTick", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, swapped);
        Assert.AreEqual(2, rewritten.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionTickProfilerHooks)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentTickTarget_IsOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // A swallow at the agent tick would skip the body that sets tickCompleted (Mission.cs:3629), so the next
        // WaitTickCompletion would spin forever: the exception leaves the method instead. That is all this entry
        // buys: the inline call (fast-forward) still reaches Mission.OnTick (docs/features/mission-perf-heartbeat.md,
        // "PatchShield").
        var target = TargetOf(typeof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch));
        Assert.IsNotNull(target, nameof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch) + " has no resolvable target.");
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
            $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // Maintainer decision D13 (2026-10-03): both stay off the exclusion list, so PatchShield attaches to them
        // whenever something patches them (Patch97 and Patch98 patch both Mission.OnTick and Mission.OnPreTick, and any
        // patch on either attaches the shield), and a foreign patch's missing-API throw there is
        // swallowed and the patch stripped instead of unwinding the application tick. That does not make an
        // interrupted Mission.OnTick safe: see docs/features/mission-perf-heartbeat.md, "PatchShield".
        foreach (var patch in new[] { typeof(Mission_OnTick_TickProfiler_Patch), typeof(Mission_OnPreTick_TickProfiler_Patch) })
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " has no resolvable target.");
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must not be in PatchShieldPolicy.ExcludedTargetMethods");
            // PatchShield skips a target on its namespace as well as on its method (PatchShield.IsExcludedTarget): a
            // prefix such as "TaleWorlds.MountAndBlade" in ExcludedTargetNamespacePrefixes would unshield both.
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(target.DeclaringType?.Namespace),
                $"{target.DeclaringType?.Namespace} must stay off ExcludedTargetNamespacePrefixes: {target.DeclaringType?.Name}.{target.Name} stays under PatchShield (decision D13)");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RequiredHooks_ResolveToTheRealTargetsAndToPatchMethodsOfTheirKind()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // The hook-health list is resolved from the patch classes' own attributes. Walk it so a renamed patch
        // method or a changed [HarmonyPatch] target fails here instead of reading as "missing" at every mission.
        var kindAttributes = new Dictionary<HarmonyPatchType, Type>
        {
            [HarmonyPatchType.Prefix] = typeof(HarmonyPrefix),
            [HarmonyPatchType.Postfix] = typeof(HarmonyPostfix),
            [HarmonyPatchType.Transpiler] = typeof(HarmonyTranspiler),
            [HarmonyPatchType.Finalizer] = typeof(HarmonyFinalizer),
        };
        var targets = new List<string>();
        foreach (var hook in MissionTickProfilerHealth.RequiredHooks())
        {
            Assert.IsNotNull(hook.Target, hook.Name + " has no resolvable target.");
            Assert.IsNotNull(hook.PatchMethod, hook.Name + " has no patch method.");
            Assert.IsTrue(hook.PatchMethod.IsDefined(kindAttributes[hook.Kind], false),
                $"{hook.Name}: {hook.PatchMethod.Name} does not carry [{kindAttributes[hook.Kind].Name}].");
            targets.Add(hook.Target.DeclaringType?.FullName + "." + hook.Target.Name);
        }
        CollectionAssert.AreEqual(
            new[]
            {
                "TaleWorlds.MountAndBlade.Mission.OnTick",
                "TaleWorlds.MountAndBlade.Mission.OnPreTick",
                "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",
                "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",
            },
            targets);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FrameBoundaryHook_IsTheOnePatchMethodThatClosesFrames()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // The health check requires the frame-boundary hook because no frame closes without it, in either mode
        // (deep review 2026-10-04). Pin that it names the one method that calls OnFrameBoundary, so a boundary moved
        // into another prefix fails here instead of leaving the check watching a method that closes nothing.
        var callers = IlCallScanner.FindCallers(typeof(MissionTickProfilerHooks).Assembly,
            m => m.DeclaringType == typeof(MissionTickProfilerHooks) && m.Name == nameof(MissionTickProfilerHooks.OnFrameBoundary),
            out var unreadable, out var scanned);

        Assert.IsTrue(scanned > 0, "No method bodies scanned: the scan failed rather than passed.");
        Assert.IsFalse(unreadable.Any(u => u.StartsWith("TAOM.Features.MissionPerf", StringComparison.Ordinal)),
            "Unreadable MissionPerf bodies: " + string.Join("; ", unreadable));
        var hook = MissionTickProfilerHealth.RequiredHooks().Single(h => h.Name == MissionTickProfilerHealth.FrameBoundaryHook);
        Assert.IsNotNull(hook.PatchMethod, hook.Name + " has no patch method.");
        CollectionAssert.AreEqual(new[] { hook.PatchMethod.DeclaringType?.FullName + "." + hook.PatchMethod.Name }, callers);
    }

    // CreatureBanditsWiringTests.TargetOf.
    private static MethodBase TargetOf(Type patch)
    {
        var byName = AccessTools.Method(patch, "TargetMethod");
        if (byName != null)
            return (MethodBase)byName.Invoke(null, null);
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }
}
