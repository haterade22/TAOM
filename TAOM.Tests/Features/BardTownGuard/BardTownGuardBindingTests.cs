using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.BardTownGuard;
using TAOM.Features.BardTownGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BardTownGuard;

/// <summary>
/// Proves the call-site swap still finds its site in the installed engine (#824). The transpiler is fail-safe, so a
/// vanilla rewrite would otherwise turn the fix off silently: the musician would crash again with every other
/// gate green.
/// </summary>
[TestClass]
public class BardTownGuardBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool IsPickCall(CodeInstruction ci) =>
        ci.operand is MethodInfo mi
        && mi.Name == "GetRandomElementWithPredicate"
        && mi.IsGenericMethod
        && mi.GetGenericArguments().SequenceEqual(new[] { typeof(Settlement) })
        && mi.GetParameters().Length == 2
        && mi.GetParameters()[0].ParameterType == typeof(MBReadOnlyList<Settlement>);

    private static bool IsHookCall(CodeInstruction ci) =>
        ci.operand is MethodInfo mi && mi.DeclaringType == typeof(BardTownPickHook) && mi.Name == nameof(BardTownPickHook.PickTown);

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTarget_ExistsAndHasExactlyOnePickCall_InTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.TargetMethod();
        Assert.IsNotNull(target, "TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();

        Assert.AreEqual(1, il.Count(IsPickCall),
            "Expected exactly one GetRandomElementWithPredicate<Settlement> call in the musician's condition.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_SwapsThatOneCallForTheHook_InTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.TargetMethod()!).ToList();
        var logger = Substitute.For<IModLogger>();

        var pickLabels = il.Single(IsPickCall).labels.ToList();
        Assert.IsTrue(pickLabels.Count > 0, "vanilla branches to the pick call, so it carries a label");

        var rewritten = BardTownTranspiler.SwapPick(il, logger).ToList();

        CollectionAssert.AreEqual(pickLabels, rewritten.Single(IsHookCall).labels, "the hook call keeps the branch target");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, rewritten.Count(IsHookCall), "exactly one call to the TAOM pick");
        Assert.AreEqual(0, rewritten.Count(IsPickCall), "no call to the engine pick may remain");
        Assert.AreEqual(il.Count, rewritten.Count, "a call-for-call swap changes no instruction count");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    public void PickTown_RunsTheEnginePickTwice_ThenNamesTheCurrentSettlement()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var il = PatchProcessor.GetOriginalInstructions(typeof(BardTownPickHook).GetMethod(nameof(BardTownPickHook.PickTown))!).ToList();
        var currentGetter = typeof(Settlement).GetProperty(nameof(Settlement.CurrentSettlement))!.GetGetMethod();

        Assert.AreEqual(2, il.Count(IsPickCall), "vanilla's pick, then the same engine pick over every other town");
        Assert.AreEqual(OpCodes.Ldarg_0, il[0].opcode, "vanilla's list first");
        Assert.AreEqual(OpCodes.Ldarg_1, il[1].opcode, "vanilla's own predicate");
        Assert.IsTrue(IsPickCall(il[2]), "so the first pick is exactly vanilla's call");
        Assert.AreEqual(1, il.Count(ci => ci.operand is MethodInfo mi && mi == currentGetter),
            "no other town: the current settlement, so .Name never reads null");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.bardtownguard.jit";
        var target = (MethodBase)TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.TargetMethod();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(BardTownGuardModule).Assembly).Apply(harmony, BardTownGuardModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            Assert.IsTrue(info!.Transpilers.Any(p => p.owner == id), "the transpiler is attached");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void PatchClass_IsInTheLiteralCategory()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var patchType = typeof(TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch);

        var categories = patchType.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { "Patch110_BardTownGuard" }, categories);
    }

    [TestMethod]
    public void TheTranspiler_WithNoMatchingCall_ReturnsTheOriginalAndWarnsOnce()
    {
        var original = new List<CodeInstruction> { new CodeInstruction(System.Reflection.Emit.OpCodes.Ret) };
        var logger = Substitute.For<IModLogger>();

        var result = BardTownTranspiler.SwapPick(original, logger).ToList();

        CollectionAssert.AreEqual(original, result);
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheTranspiler_WithNoMatchingCall_ReportsTheBardTownGuardOnce()
    {
        var saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        try
        {
            var original = new List<CodeInstruction> { new CodeInstruction(System.Reflection.Emit.OpCodes.Ret) };

            BardTownTranspiler.SwapPick(original, Substitute.For<IModLogger>()).ToList();
            BardTownTranspiler.SwapPick(original, Substitute.For<IModLogger>()).ToList();

            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch110_BardTownGuard", taken[0].Id);
            StringAssert.Contains(taken[0].Reason, "found 0");
        }
        finally
        {
            SelfDisables.Ledger = saved;
        }
    }

    [TestMethod]
    public void TheTranspiler_WithTwoMatchingCalls_ReturnsTheOriginalUnchangedAndWarnsOnce()
    {
        var pick = typeof(TaleWorlds.Core.Extensions).GetMethods()
            .First(m => m.Name == "GetRandomElementWithPredicate" && m.IsGenericMethodDefinition
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(MBReadOnlyList<>))
            .MakeGenericMethod(typeof(Settlement));
        var original = new List<CodeInstruction>
        {
            new CodeInstruction(System.Reflection.Emit.OpCodes.Call, pick),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Call, pick),
            new CodeInstruction(System.Reflection.Emit.OpCodes.Ret),
        };
        var logger = Substitute.For<IModLogger>();

        var result = BardTownTranspiler.SwapPick(original, logger).ToList();

        Assert.IsTrue(result.All(ci => !IsHookCall(ci)), "never a half-patched method");
        Assert.AreEqual(2, result.Count(IsPickCall));
        logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
