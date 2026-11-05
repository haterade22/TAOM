using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// Pins what Patch103 depends on in the installed engine (v1.5.4 at the time of writing): the six call sites it
/// swaps in <c>Mission.AfterStart</c> (each exactly once, on the real IL), the target signatures, the helpers that
/// replace them, the patch methods' parameter names (Harmony binds by name), the category and phase, PatchShield's
/// reach, and the MCM toggle. The IL tests carry <c>RequiresGameIL</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionStartGuardBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo AfterStart() => AccessTools.Method(typeof(Mission), nameof(Mission.AfterStart), Type.EmptyTypes);

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void Target_MissionAfterStart_IsAPublicNonVirtualVoidInstanceMethodWithNoParameters()
    {
        RequireGame();

        var target = AfterStart();

        Assert.IsNotNull(target, "Mission.AfterStart() did not resolve.");
        Assert.IsTrue(target.IsPublic);
        Assert.IsFalse(target.IsStatic);
        Assert.IsFalse(target.IsVirtual, "a patch on a virtual never reaches an override");
        Assert.AreEqual(typeof(void), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void SwapTargets_AreTheSixEngineStartCalls_WithTheirExactSignatures()
    {
        RequireGame();

        var swaps = MissionStartGuardSwaps.Build();

        var actual = swaps.Select(s => Describe(s.Target)).ToList();
        CollectionAssert.AreEqual(new[]
        {
            "MBSubModuleBase.OnBeforeMissionBehaviorInitialize(Mission):Void",
            "MissionBehavior.OnBehaviorInitialize():Void",
            "MBSubModuleBase.OnMissionBehaviorInitialize(Mission):Void",
            "MissionBehavior.EarlyStart():Void",
            "MissionBehavior.AfterStart():Void",
            "MissionObject.AfterMissionStart():Void",
        }, actual);
        Assert.IsTrue(swaps.All(s => s.Target.IsVirtual && !s.Target.IsStatic), "all six are public virtual instance methods");
        Assert.IsTrue(swaps.All(s => s.Target.IsPublic));
    }

    private static string Describe(MethodInfo m) =>
        m.DeclaringType!.Name + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) + "):" + m.ReturnType.Name;

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void SwapHelpers_AreStaticOnTheCallsClass_InstanceFirstThenTheTargetsArguments()
    {
        RequireGame();

        Assert.AreEqual(6, MissionStartGuardSwaps.Build().Count, "six swaps, one per wrapped start call");
        foreach (var swap in MissionStartGuardSwaps.Build())
        {
            var helper = swap.Helpers.Single();
            Assert.IsTrue(helper.IsStatic, helper.Name);
            Assert.AreEqual(typeof(MissionStartGuardCalls), helper.DeclaringType);
            Assert.AreEqual(swap.Target.Name, helper.Name, "a helper carries the name of the call it replaces");
            var expected = new[] { swap.Target.DeclaringType }.Concat(swap.Target.GetParameters().Select(p => p.ParameterType)).ToArray();
            CollectionAssert.AreEqual(expected, helper.GetParameters().Select(p => p.ParameterType).ToArray(), helper.Name);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MissionAfterStart_HoldsEachSwappedCallExactlyOnce_InTheInstalledEngine()
    {
        RequireGame();

        var il = PatchProcessor.GetOriginalInstructions(AfterStart()).ToList();

        Assert.AreEqual(6, MissionStartGuardSwaps.Build().Count, "six swaps, one per wrapped start call");
        foreach (var swap in MissionStartGuardSwaps.Build())
            Assert.AreEqual(1, il.Count(ci => ci.Calls(swap.Target)), Describe(swap.Target));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Rewrite_OnTheInstalledMissionAfterStart_SwapsExactlySixSitesWithNoWarning()
    {
        RequireGame();
        var logger = Substitute.For<IModLogger>();
        var original = PatchProcessor.GetOriginalInstructions(AfterStart()).ToList();

        var rewritten = TickProfilerTranspiler.Rewrite(original, MissionStartGuardSwaps.Build(), "Mission.AfterStart", logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(MissionStartGuardService.ExpectedSites, swapped);
        Assert.AreEqual(6, swapped);
        Assert.AreEqual(original.Count, rewritten.Count, "a swap inserts and removes nothing");
        Assert.AreEqual(6, rewritten.Count(ci => ci.opcode == System.Reflection.Emit.OpCodes.Call
                                                  && ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionStartGuardCalls)));
        foreach (var swap in MissionStartGuardSwaps.Build())
            Assert.AreEqual(0, rewritten.Count(ci => ci.Calls(swap.Target)), "the original call is gone: " + Describe(swap.Target));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Rewrite_RunTwice_SecondPassFindsNothingAndLeavesTheFirstPassesIlAlone()
    {
        RequireGame();
        var first = TickProfilerTranspiler.Rewrite(PatchProcessor.GetOriginalInstructions(AfterStart()).ToList(),
            MissionStartGuardSwaps.Build(), "Mission.AfterStart", null, out _);

        var second = TickProfilerTranspiler.Rewrite(first, MissionStartGuardSwaps.Build(), "Mission.AfterStart", null, out var swapped);

        Assert.AreEqual(0, swapped, "idempotent: the re-applied category soft-fails to the already-swapped IL");
        Assert.AreEqual(6, second.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(MissionStartGuardCalls)));
    }

    // The rewrite is checked as IL above. This applies the real patch class to the real method, so Harmony builds the
    // replacement and the JIT compiles it (an InvalidProgramException from a mis-shaped swap would surface here), then
    // takes it off again. The method is never run: a Mission cannot be built in a test host.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        RequireGame();
        const string id = "taom.tests.missionstartguard.jit";
        var harmony = new Harmony(id);
        try
        {
            MissionStartGuardSwaps.LastSwapped = 0;

            harmony.CreateClassProcessor(PatchType).Patch();

            var info = Harmony.GetPatchInfo(AfterStart());
            Assert.IsTrue(info.Transpilers.Any(p => p.owner == id), "the transpiler is attached");
            Assert.IsTrue(info.Finalizers.Any(p => p.owner == id), "the finalizer is attached");
            Assert.AreEqual(MissionStartGuardService.ExpectedSites, MissionStartGuardSwaps.LastSwapped, "the transpiler swapped every site when Harmony ran it");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(AfterStart().MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
            MissionStartGuardSwaps.LastSwapped = 0;
        }
    }

    // ---- The patch class ----

    private static readonly Type PatchType = typeof(Mission_AfterStart_MissionStartGuard_Patch);

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void PatchClass_TargetsMissionAfterStart_InTheLiteralCategory()
    {
        RequireGame();

        var info = HarmonyMethod.Merge(PatchType.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        var categories = PatchType.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        Assert.AreEqual(typeof(Mission), info.declaringType);
        Assert.AreEqual(nameof(Mission.AfterStart), info.methodName);
        CollectionAssert.AreEqual(new[] { "Patch103_MissionStartGuard" }, categories);
        Assert.AreEqual("Patch103_MissionStartGuard", MissionStartGuardModule.PatchCategory);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchMethods_HaveTheNamesAndShapesHarmonyBindsBy()
    {
        var prefix = AccessTools.Method(PatchType, "Prefix");
        var transpiler = AccessTools.Method(PatchType, "Transpiler");
        var finalizer = AccessTools.Method(PatchType, "Finalizer");

        Assert.IsNotNull(prefix);
        Assert.IsNotNull(transpiler);
        Assert.IsNotNull(finalizer);
        Assert.AreEqual(typeof(void), prefix.ReturnType, "a void prefix cannot skip the original");
        CollectionAssert.AreEqual(new[] { "__instance", "__state" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[1].IsOut, "__state is out on the prefix: it carries this call's 'the prefix ran'");
        Assert.AreEqual(typeof(bool), prefix.GetParameters()[1].ParameterType.GetElementType());
        Assert.IsNotNull(prefix.GetCustomAttribute<HarmonyPrefix>());
        Assert.AreEqual(Priority.First, prefix.GetCustomAttribute<HarmonyPriority>()!.info.priority,
            "ahead of every lower-priority prefix; an earlier First, a higher raw priority or HarmonyBefore still runs first");
        Assert.IsNotNull(transpiler.GetCustomAttribute<HarmonyTranspiler>());
        CollectionAssert.AreEqual(new[] { "instructions" }, transpiler.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(void), finalizer.ReturnType, "a void finalizer keeps Harmony's rethrow and the engine's stack");
        CollectionAssert.AreEqual(new[] { "__exception", "__state" }, finalizer.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(Exception), finalizer.GetParameters()[0].ParameterType);
        Assert.AreEqual(typeof(bool), finalizer.GetParameters()[1].ParameterType, "by value: Harmony rebinds __state by type and name");
        Assert.AreEqual(Priority.First, finalizer.GetCustomAttribute<HarmonyPriority>()!.info.priority,
            "runs before PatchShield's finalizer so it sees the raw exception");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Cleanup_IsAStaticVoidHarmonyCleanupTakingTheApplyException()
    {
        var cleanup = AccessTools.Method(PatchType, "Cleanup");

        Assert.IsNotNull(cleanup);
        Assert.IsTrue(cleanup.IsStatic);
        Assert.AreEqual(typeof(void), cleanup.ReturnType, "a non-void cleanup would replace Harmony's exception");
        Assert.AreEqual(1, cleanup.GetParameters().Length);
        Assert.AreEqual(typeof(Exception), cleanup.GetParameters()[0].ParameterType, "Harmony matches by type");
        Assert.IsNotNull(cleanup.GetCustomAttribute<HarmonyCleanup>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_HasNoBoolPrefix_SoItCannotSkipTheEnginesMethod()
    {
        var boolPrefixes = PatchType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "Prefix" && m.ReturnType == typeof(bool));

        Assert.AreEqual(0, boolPrefixes.Count());
    }

    // ---- Wiring ----

    [TestMethod]
    public void Module_DeclaresPatch103AtGameInit_AndIsInTheModuleList()
    {
        var module = FeatureModules.All.OfType<MissionStartGuardModule>().SingleOrDefault();

        Assert.IsNotNull(module, "MissionStartGuardModule is not in FeatureModules.All.");
        Assert.AreEqual("MissionStartGuard", module.Id);
        Assert.IsNull(module.ParkedReason);
        var decl = module.PatchCategories.Single();
        Assert.AreEqual("Patch103_MissionStartGuard", decl.Category);
        Assert.AreEqual(ApplyPhase.GameInit, decl.Phase,
            "game init precedes every mission, and Patch43 patches the same method at that point");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchShield_StillWrapsMissionAfterStart_ByNameAndByNamespace()
    {
        // Once per mission, main thread: stays under the shield (decision D13). Its finalizer composes with the void one (registry entry).
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(typeof(Mission).FullName ?? "TaleWorlds.MountAndBlade.Mission", "AfterStart"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace("TaleWorlds.MountAndBlade"));
    }

    // ---- The MCM toggle ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Setting_SurviveMissionStartFailures_IsAMasterGroupBoolThatNeedsNoRestart()
    {
        var property = typeof(CrashReportSettings).GetProperty("SurviveMissionStartFailures");

        Assert.IsNotNull(property, "CrashReportSettings.SurviveMissionStartFailures is missing.");
        Assert.AreEqual(typeof(bool), property.PropertyType);
        var bool_ = property.GetCustomAttribute<SettingPropertyBoolAttribute>();
        Assert.IsNotNull(bool_);
        Assert.AreEqual("Survive Mission Start Failures", bool_.DisplayName);
        Assert.IsFalse(bool_.RequireRestart, "read when an exception arrives");
        Assert.IsFalse(string.IsNullOrWhiteSpace(bool_.HintText));
        Assert.AreEqual("Master", property.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
    }

    [TestMethod]
    public void SettingsProvider_NoMcmInstance_DefaultsToSurviving()
    {
        Assert.IsTrue(new MissionStartGuardSettingsProvider().SurviveMissionStartFailures);
    }
}
