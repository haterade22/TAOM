using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.LinQuick;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Patch169: the battle-end hero kill loop walks a lazy <c>WhereQ</c> over the roster's one cached troop list, and the
/// first kill rebuilds that list shorter mid-walk (ArgumentOutOfRangeException, player crash report). The transpiler
/// swaps the one call for a helper that walks a private copy.
/// </summary>
[TestClass]
public class Patch169BattleEndHeroDeathSnapshotTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void VanillaWhereQ_OverAListThatShrinksMidWalk_Throws()
    {
        var source = new List<int> { 1, 2, 3, 4 };
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() =>
        {
            foreach (var _ in source.WhereQ(x => true))
                source.RemoveAt(source.Count - 1);
        });
    }

    [TestMethod]
    public void SnapshotWhere_OverAListThatShrinksMidWalk_VisitsEveryOriginalMatchInOrder()
    {
        var source = new List<int> { 1, 2, 3, 4, 5, 6 };
        var seen = new List<int>();

        foreach (var x in BattleEndHeroDeathTranspiler.SnapshotWhere(source, x => x % 2 == 0))
        {
            seen.Add(x);
            source.Clear();
        }

        CollectionAssert.AreEqual(new[] { 2, 4, 6 }, seen);
    }

    [TestMethod]
    public void SnapshotWhere_KeepsTheFilterLazy_SoALaterChangeStillDecides()
    {
        var source = new List<int> { 1, 2, 3 };
        var skip = new HashSet<int>();
        var seen = new List<int>();

        foreach (var x in BattleEndHeroDeathTranspiler.SnapshotWhere(source, x => !skip.Contains(x)))
        {
            seen.Add(x);
            skip.Add(x + 1);
        }

        CollectionAssert.AreEqual(new[] { 1, 3 }, seen, "an element a visit disqualified is skipped, as in vanilla");
    }

    private static MethodInfo Vanilla()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Assert.IsNotNull(BattleEndHeroDeathTranspiler.VanillaCall, "LinQuick.WhereQ(List, Func) not found on the installed engine: Patch169 would self-disable");
        return BattleEndHeroDeathTranspiler.VanillaCall!;
    }

    private static List<CodeInstruction> Stream(MethodInfo call, int sites)
    {
        var list = new List<CodeInstruction> { new(OpCodes.Ldarg_1), new(OpCodes.Ldnull) };
        for (int i = 0; i < sites; i++) list.Add(new CodeInstruction(OpCodes.Call, call));
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    [TestMethod]
    public void OneSite_IsSwappedForTheHelper_AndNothingElseChanges()
    {
        var vanilla = Vanilla();
        var logger = Substitute.For<IModLogger>();
        var input = Stream(vanilla, 1);

        var output = BattleEndHeroDeathTranspiler.Rewrite(input, logger);

        Assert.AreEqual(input.Count, output.Count);
        Assert.AreSame(BattleEndHeroDeathTranspiler.HookCall, output[2].operand);
        Assert.AreEqual(OpCodes.Call, output[2].opcode);
        Assert.IsFalse(output.Any(ci => ci.operand is MethodInfo mi && mi == vanilla));
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void AnyOtherSiteCount_ReturnsTheStreamUntouched_WarnsOnce_AndReports(int sites)
    {
        var vanilla = Vanilla();
        var logger = Substitute.For<IModLogger>();
        var saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
        try
        {
            var input = Stream(vanilla, sites);
            var before = input.Select(ci => (ci.opcode, ci.operand)).ToList();

            var output = BattleEndHeroDeathTranspiler.Rewrite(input, logger);

            CollectionAssert.AreEqual(before, output.Select(ci => (ci.opcode, ci.operand)).ToList());
            logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("[MapEventGuard]")));
            var taken = SelfDisables.Ledger.Take();
            Assert.AreEqual(1, taken.Count);
            Assert.AreEqual(BattleEndHeroDeathTranspiler.Category, taken[0].Id);
        }
        finally
        {
            SelfDisables.Ledger = saved;
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void InstalledEngine_HasExactlyOneWhereQSite_AndItIsSwapped()
    {
        Vanilla();
        var target = Patch169_BattleEndHeroDeathSnapshot.TargetMethod();
        Assert.IsNotNull(target, "MapEventSide.HandleMapEventEndForPartyInternal did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target).ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = BattleEndHeroDeathTranspiler.Rewrite(il, logger);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, rewritten.Count(ci => ci.operand is MethodInfo mi && mi == BattleEndHeroDeathTranspiler.HookCall));
        Assert.AreEqual(0, rewritten.Count(ci => ci.operand is MethodInfo mi && mi == BattleEndHeroDeathTranspiler.VanillaCall));
        Assert.AreEqual(il.Count, rewritten.Count);
    }
}
