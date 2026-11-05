using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Patch141's M5 targets (build-spec 9.1 and 9.3). Harmony binds parameter names, three targets are private, and the spacing transpiler swaps one call per method, so
/// each binding is pinned here and the swap is counted in the installed engine's IL.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalSquareAndSpacingPatchTests
{
    private static T Blank<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static MethodInfo HasAnyMounted() => typeof(Formation).GetProperty(nameof(Formation.HasAnyMountedUnit))!.GetGetMethod()!;

    private static MethodInfo SignificantMounted() => typeof(Formation).GetProperty(nameof(Formation.CalculateHasSignificantNumberOfMounted))!.GetGetMethod()!;

    // ---- the square ----

    [TestMethod]
    public void TheSquarePrefixes_BindTheNamesHarmonyInjects_OnTheirEngineTargets()
    {
        var arrangement = typeof(ArrangementOrder).GetMethod(nameof(ArrangementOrder.GetArrangement))!;
        CollectionAssert.AreEqual(new[] { "formation" }, arrangement.GetParameters().Select(p => p.Name).ToArray());
        var prefix = typeof(MarshalSquareArrangementPatch).GetMethod(nameof(MarshalSquareArrangementPatch.Prefix))!;
        CollectionAssert.AreEqual(new[] { "__instance", "formation", "__result" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[0].ParameterType.IsByRef, "a struct instance is injected by reference");
        Assert.IsTrue(typeof(ArrangementOrder).IsValueType);

        var reapply = typeof(Formation).GetMethod("ReapplyFormOrder", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(reapply);
        Assert.AreEqual(0, reapply!.GetParameters().Length);
        CollectionAssert.AreEqual(new[] { "__instance" }, typeof(MarshalSquareReapplyPatch).GetMethod("Prefix")!.GetParameters().Select(p => p.Name).ToArray());

        var width = typeof(Formation).GetMethod("CalculateDesiredWidth", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(width);
        Assert.AreEqual(typeof(float), width!.ReturnType);
        CollectionAssert.AreEqual(new[] { "__instance", "__result" }, typeof(MarshalSquareWidthPatch).GetMethod("Prefix")!.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    public void TheSquareArrangementPrefix_NotLive_FallsThroughToVanilla()
    {
        MarshalHooks.Clear();
        var order = ArrangementOrder.ArrangementOrderSquare;
        IFormationArrangement result = null!;

        Assert.IsTrue(MarshalSquareArrangementPatch.Prefix(ref order, Blank<Formation>(), ref result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TheSquareArrangementPrefix_LiveButAnotherOrder_FallsThroughToVanilla()
    {
        MarshalHooks.Begin(new Sink());
        MarshalHooks.Arm(false, false, true, true, hollowSquare: true);
        try
        {
            var order = ArrangementOrder.ArrangementOrderLine;
            IFormationArrangement result = null!;

            Assert.IsTrue(MarshalSquareArrangementPatch.Prefix(ref order, Blank<Formation>(), ref result));
        }
        finally
        {
            MarshalHooks.Clear();
        }
    }

    [TestMethod]
    public void TheSquareArrangementPrefix_LiveSquareButASettingOffOrNoTeam_FallsThroughToVanilla()
    {
        var order = ArrangementOrder.ArrangementOrderSquare;
        IFormationArrangement result = null!;
        MarshalHooks.Begin(new Sink());
        try
        {
            MarshalHooks.Arm(false, false, true, true, hollowSquare: false);
            Assert.IsTrue(MarshalSquareArrangementPatch.Prefix(ref order, Blank<Formation>(), ref result), "the setting is off");

            MarshalHooks.Arm(false, false, true, true, hollowSquare: true);
            Assert.IsTrue(MarshalSquareArrangementPatch.Prefix(ref order, Blank<Formation>(), ref result), "a formation with no team is a simulation copy");
            Assert.IsNull(result);
        }
        finally
        {
            MarshalHooks.Clear();
        }
    }

    [TestMethod]
    public void TheWidthPrefixes_AnArrangementThatIsNotThePlainSquare_FallThroughToVanilla()
    {
        var formation = Blank<Formation>();
        var width = 0f;

        Assert.IsTrue(MarshalSquareReapplyPatch.Prefix(formation), "a formation with no arrangement");
        Assert.IsTrue(MarshalSquareWidthPatch.Prefix(formation, ref width));
        Assert.AreEqual(0f, width);
    }

    // ---- the spacing repair ----

    [TestMethod]
    public void TheSpacingTargets_AreTheFourLimitGettersAndTheThreePublicOverloads()
    {
        var targets = MarshalSpacingPatch.TargetMethods().ToList();

        Assert.AreEqual(7, targets.Count);
        CollectionAssert.AreEquivalent(
            new[] { "get_MinimumDistance", "get_MaximumDistance", "get_MinimumInterval", "get_MaximumInterval" },
            targets.Where(t => t.Name.StartsWith("get_", StringComparison.Ordinal)).Select(t => t.Name).ToArray());
        Assert.AreEqual(3, targets.Count(t => t.Name == "GetUnitPositionWithIndexAccordingToNewOrder" && !t.IsStatic && t.IsPublic));
    }

    [TestMethod]
    public void TheTranspiler_OneCallOfTheOldGetter_IsSwappedForTheSignificantNumberGetter()
    {
        var list = new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, HasAnyMounted()), new CodeInstruction(OpCodes.Ret) };

        var result = MarshalSpacingPatch.Transpiler(list, TargetOf("get_MinimumDistance")).ToList();

        Assert.AreEqual(SignificantMounted(), result[1].operand);
        Assert.AreEqual(OpCodes.Call, result[1].opcode);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void TheTranspiler_NoCallOrTwoCalls_LeavesTheMethodAsItIs_AndNeverThrows()
    {
        var none = new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ret) };
        var two = new[] { new CodeInstruction(OpCodes.Call, HasAnyMounted()), new CodeInstruction(OpCodes.Call, HasAnyMounted()), new CodeInstruction(OpCodes.Ret) };

        var untouched = MarshalSpacingPatch.Transpiler(none, TargetOf("get_MinimumDistance")).ToList();
        var both = MarshalSpacingPatch.Transpiler(two, TargetOf("get_MinimumDistance")).ToList();

        Assert.AreEqual(2, untouched.Count);
        Assert.IsTrue(both.Take(2).All(i => Equals(i.operand, HasAnyMounted())), "two calls are not what the repair expects, so neither is swapped");
    }

    [TestMethod]
    public void TheTranspiler_AppliedTwice_FindsNothingTheSecondTime()
    {
        var list = new[] { new CodeInstruction(OpCodes.Call, HasAnyMounted()), new CodeInstruction(OpCodes.Ret) };

        var once = MarshalSpacingPatch.Transpiler(list, TargetOf("get_MinimumDistance")).ToList();
        var twice = MarshalSpacingPatch.Transpiler(once, TargetOf("get_MinimumDistance")).ToList();

        Assert.AreEqual(SignificantMounted(), twice[0].operand);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EachOfTheSevenTargets_HasExactlyOneCallOfHasAnyMountedUnit_InTheInstalledEngine()
    {
        if (!GameAssemblies.EnsureLoaded()) Assert.Inconclusive("Game assemblies unavailable.");

        foreach (var target in MarshalSpacingPatch.TargetMethods())
        {
            var il = PatchProcessor.GetOriginalInstructions(target).ToList();
            Assert.AreEqual(1, il.Count(i => Equals(i.operand, HasAnyMounted())), target.DeclaringType!.Name + "." + target.Name + " must call HasAnyMountedUnit once");
            Assert.AreEqual(0, il.Count(i => Equals(i.operand, SignificantMounted())), target.Name + " already reads the significant number: the swap would be a no-op");
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_OverTheRealIlOfEachTarget_LeavesOneCallOfTheSignificantNumberGetterAndNoneOfTheOld()
    {
        if (!GameAssemblies.EnsureLoaded()) Assert.Inconclusive("Game assemblies unavailable.");

        foreach (var target in MarshalSpacingPatch.TargetMethods())
        {
            var swapped = MarshalSpacingPatch.Transpiler(PatchProcessor.GetOriginalInstructions(target), target).ToList();

            Assert.AreEqual(0, swapped.Count(i => Equals(i.operand, HasAnyMounted())), target.Name);
            Assert.AreEqual(1, swapped.Count(i => Equals(i.operand, SignificantMounted())), target.Name);
        }
    }

    [TestMethod]
    public void Prepare_WhenTheSettingsCannotBeRead_AppliesNothing_AndNamesWhy()
    {
        var applies = MarshalSpacingPatch.Prepare();

        Assert.IsFalse(applies, "a patch whose gate could not be read is not applied");
        Assert.AreEqual("prepare-failed", MarshalSpacingPatch.WhyNot);
        MarshalSpacingPatch.WhyNot = "not-prepared";
    }

    [TestMethod]
    public void TheSpacingRepair_LogsADegradedMethodAtErrorLevel_UnderTheTagTheTrialJudgeReads()
    {
        // A method left as it is leaves the four limits reading cavalry for a company with one rider, and only an error line reaches the judge (MARSHAL_ERROR and the manifest's fail lists).
        var code = System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalSpacingPatch.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(code, "LogError(\"[MarshalsWord] the spacing repair \" + detail);");
        StringAssert.Contains(code, "LogDegraded($\"found {sites.Count} calls of HasAnyMountedUnit in {original?.Name} where it expects one, so that method is left as it is.\");");
        Assert.IsFalse(code.Contains("LogWarning"), "a warning reads as a pass");
    }

    [TestMethod]
    public void TheSpacingOverloads_AreFoundByTheEnginesOwnName_SoARenameFailsTheBuildAndNotTheGame()
    {
        var code = System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalSpacingPatch.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(code, "m.Name == nameof(Formation.GetUnitPositionWithIndexAccordingToNewOrder)");
        Assert.IsFalse(code.Contains("\"GetUnitPositionWithIndexAccordingToNewOrder\""), "no literal copy of the name");
    }

    [TestMethod]
    public void TheSquareArrangementPrefix_TestsLiveSettingAndOrderBeforeAnythingElse()
    {
        // A positive path needs a live team, which a unit run does not have, so the order of the first tests is pinned in the source.
        var code = System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalSquareArrangementPatch.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(code, "if (!MarshalHooks.IsLive || !MarshalHooks.HollowSquare || __instance.OrderEnum != ArrangementOrder.ArrangementOrderEnum.Square) return true;");
        StringAssert.Contains(code, "if (team == null || team != Mission.Current?.PlayerTeam || formation!.IsAIControlled) return true;");
        StringAssert.Contains(code, "__result = new SquareFormation(formation); return false;");
    }

    private static MethodBase TargetOf(string name) => MarshalSpacingPatch.TargetMethods().First(m => m.Name == name);

    private sealed class Sink : IMarshalHookSink
    {
        public bool TryCapture(MarshalOrderPayload payload) => false;

        public void ArraySolved(bool click, int companies)
        {
        }

        public void ArrayFault(Exception exception)
        {
        }

        public void MarchRequested(System.Collections.Generic.IReadOnlyList<MarchTarget> targets)
        {
        }

        public void PanelPress(PanelAction action, System.Collections.Generic.IReadOnlyList<int> companies)
        {
        }
    }
}
