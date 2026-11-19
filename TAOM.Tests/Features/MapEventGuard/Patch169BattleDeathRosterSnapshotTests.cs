using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.LinQuick;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// #1475. <c>MapEventSide.HandleMapEventEndForPartyInternal</c> kills each <c>DiedInBattle</c> hero while a lazy
/// <c>WhereQ</c> is still indexing the roster's cached list; anything that calls <c>GetTroopRoster()</c> on that
/// roster mid-loop refills the list one element shorter and the loop reads past its end. Patch169 inserts one
/// <c>Materialize</c> call after the <c>WhereQ</c>, so the loop walks a filled copy, and keeps the <c>WhereQ</c> for
/// Coop's transpiler on the same method, which throws when it cannot find it.
/// </summary>
[TestClass]
public class Patch169BattleDeathRosterSnapshotTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool IsWhereQ(CodeInstruction ci) =>
        ci.operand is MethodInfo mi
        && mi.Name == "WhereQ"
        && mi.DeclaringType == typeof(LinQuick)
        && mi.IsGenericMethod
        && mi.GetGenericArguments().SequenceEqual(new[] { typeof(TroopRosterElement) })
        && mi.GetParameters()[0].ParameterType == typeof(List<TroopRosterElement>);

    private static bool IsSnapshotCall(CodeInstruction ci) =>
        ci.operand is MethodInfo mi && mi.DeclaringType == typeof(BattleDeathRosterTranspiler)
                                    && mi.Name == nameof(BattleDeathRosterTranspiler.Materialize);

    private static bool IsToArray(CodeInstruction ci) =>
        ci.operand is MethodInfo mi && mi.DeclaringType == typeof(Enumerable) && mi.Name == nameof(Enumerable.ToArray);

    /// <summary>
    /// The matcher of Coop's own transpiler on this method (BannerlordCoop
    /// GameInterface/Services/MapEventSides/Patches/MapEventSidePatches.cs): the first call named WhereQ with the
    /// generic argument TroopRosterElement gets Enumerable.ToArray after it; none found throws.
    /// </summary>
    private static List<CodeInstruction> CoopShapedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var toArray = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(Enumerable.ToArray) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(TroopRosterElement));
        var result = new List<CodeInstruction>();
        var found = false;
        foreach (var ci in instructions)
        {
            result.Add(ci);
            if (found || !(ci.operand is MethodInfo mi) || mi.Name != "WhereQ" || !mi.IsGenericMethod
                || mi.GetGenericArguments()[0] != typeof(TroopRosterElement))
                continue;
            result.Add(new CodeInstruction(OpCodes.Call, toArray));
            found = true;
        }

        if (!found)
            throw new InvalidOperationException("Coop: could not find the death-marked hero query.");
        return result;
    }

    /// <summary>Kills the first match the way MakeDead does: the list loses an element while the loop runs.</summary>
    private static List<int> DrainWhileShrinking(IEnumerable<int> matches, List<int> source)
    {
        var seen = new List<int>();
        foreach (var x in matches)
        {
            seen.Add(x);
            if (seen.Count == 1)
                source.RemoveAt(0);
        }

        return seen;
    }

    [TestMethod]
    public void EngineWhereQ_ThrowsWhenTheListShrinksMidLoop()
    {
        var roster = new List<int> { 1, 2, 3, 4 };

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => DrainWhileShrinking(roster.WhereQ(x => x % 2 == 1), roster));
    }

    [TestMethod]
    public void Materialize_FinishesAndYieldsEveryMatch_WhenTheListShrinksMidLoop()
    {
        var roster = new List<int> { 1, 2, 3, 4 };

        var seen = DrainWhileShrinking(BattleDeathRosterTranspiler.Materialize(roster.WhereQ(x => x % 2 == 1)), roster);

        CollectionAssert.AreEqual(new[] { 1, 3 }, seen);
    }

    [TestMethod]
    public void Materialize_KeepsOrder_AndReadsThePredicateOncePerElement()
    {
        var calls = 0;

        var result = BattleDeathRosterTranspiler.Materialize(new List<int> { 5, 6, 7 }.WhereQ(x => { calls++; return x != 6; })).ToList();

        CollectionAssert.AreEqual(new[] { 5, 7 }, result);
        Assert.AreEqual(3, calls);
    }

    [TestMethod]
    public void Materialize_TakesAndReturnsTheWhereQResultType_SoTheStackIsUnchanged()
    {
        var engine = typeof(LinQuick).GetMethods().Single(m => m.Name == "WhereQ" && m.IsGenericMethodDefinition
            && m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>));
        var ours = typeof(BattleDeathRosterTranspiler).GetMethod(nameof(BattleDeathRosterTranspiler.Materialize))!;

        Assert.IsTrue(ours.IsStatic && ours.IsGenericMethodDefinition);
        Assert.AreEqual(engine.ReturnType.GetGenericTypeDefinition(), ours.ReturnType.GetGenericTypeDefinition());
        Assert.AreEqual(engine.ReturnType.GetGenericTypeDefinition(), ours.GetParameters().Single().ParameterType.GetGenericTypeDefinition());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTarget_HasExactlyOneRosterWhereQ_InTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch169_BattleDeathRosterSnapshot.TargetMethod();
        Assert.IsNotNull(target, "MapEventSide.HandleMapEventEndForPartyInternal did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.AreEqual(1, il.Count(IsWhereQ));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_InsertsOneMaterializeAfterThatCall_InTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var il = PatchProcessor.GetOriginalInstructions(Patch169_BattleDeathRosterSnapshot.TargetMethod()!).ToList();
        var labels = il.Single(IsWhereQ).labels.ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = BattleDeathRosterTranspiler.InsertSnapshot(il, logger).ToList();

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        var at = rewritten.FindIndex(IsWhereQ);
        Assert.AreEqual(1, rewritten.Count(IsWhereQ), "the engine WhereQ stays for Coop's matcher");
        Assert.IsTrue(IsSnapshotCall(rewritten[at + 1]), "Materialize follows the WhereQ directly");
        Assert.AreEqual(1, rewritten.Count(IsSnapshotCall));
        Assert.AreEqual(0, rewritten[at + 1].labels.Count, "no branch lands between the two calls");
        CollectionAssert.AreEqual(labels, rewritten[at].labels, "the WhereQ keeps its labels");
        Assert.AreEqual(il.Count + 1, rewritten.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_AndCoopsTranspiler_BothApply_InEitherOrder()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var il = PatchProcessor.GetOriginalInstructions(Patch169_BattleDeathRosterSnapshot.TargetMethod()!).ToList();
        var logger = Substitute.For<IModLogger>();

        // Neither transpiler changes an instruction it is given, so both orders can share the original stream.
        var taomFirst = CoopShapedTranspiler(BattleDeathRosterTranspiler.InsertSnapshot(il, logger));
        var coopFirst = BattleDeathRosterTranspiler.InsertSnapshot(CoopShapedTranspiler(il), logger).ToList();

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        foreach (var (result, first, second) in new[]
                 {
                     (taomFirst, (Func<CodeInstruction, bool>)IsToArray, (Func<CodeInstruction, bool>)IsSnapshotCall),
                     (coopFirst, IsSnapshotCall, IsToArray),
                 })
        {
            var at = result.FindIndex(IsWhereQ);
            Assert.IsTrue(first(result[at + 1]) && second(result[at + 2]), "both calls follow the WhereQ, the later transpiler's first");
            Assert.AreEqual(il.Count + 2, result.Count);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.patch169.jit";
        var target = Patch169_BattleDeathRosterSnapshot.TargetMethod()!;
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(EncounterGuardModule).Assembly).Apply(harmony, EncounterGuardModule.BattleDeathSnapshotCategory);

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
    public void TheTranspiler_WithNoMatchingCall_ReturnsTheOriginal_WarnsAndReportsOnce()
    {
        var saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        try
        {
            var original = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ret) };
            var logger = Substitute.For<IModLogger>();

            var result = BattleDeathRosterTranspiler.InsertSnapshot(original, logger).ToList();

            CollectionAssert.AreEqual(original, result);
            logger.Received(1).LogWarning(Arg.Any<string>());
            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual("Patch169_BattleDeathRosterSnapshot", taken[0].Id);
        }
        finally
        {
            SelfDisables.Ledger = saved;
        }
    }

    [TestMethod]
    public void TheModule_AppliesPatch169AtGameInit()
    {
        var decl = new EncounterGuardModule().PatchCategories.Single(d => d.Category == "Patch169_BattleDeathRosterSnapshot");

        Assert.AreEqual(ApplyPhase.GameInit, decl.Phase);
    }
}
