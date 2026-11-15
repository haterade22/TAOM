using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MountReins;
using TAOM.Features.MountReins.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MountReins;

/// <summary>
/// The rule and the call-site swap of the mount reins guard (Patch153). A harness with reins on a mount whose Monster
/// has no rein skeleton crashed a player's mounted spawn (player case 2026-11-15); the transpiler is fail-safe, so these
/// tests are what notices a matcher that stops matching.
/// </summary>
[TestClass]
public class MountReinsTranspilerTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("horse_harness_rein_skel", true)]
    [DataRow("camel_rein_skeleton", true)]
    public void Rule_AllowsTheCloth_OnlyWithAReinSkeleton(string? reinSkeleton, bool expected)
        => Assert.AreEqual(expected, MountReinsRule.CanBuildReinsCloth(reinSkeleton));

    private static MethodInfo Vanilla()
    {
        if (!_gameLoaded || MountReinsTranspiler.VanillaCall == null) Assert.Inconclusive("Game assemblies unavailable.");
        return MountReinsTranspiler.VanillaCall!;
    }

    private static List<CodeInstruction> Stream(MethodInfo call, int sites, Label? label = null)
    {
        var list = new List<CodeInstruction> { new(OpCodes.Ldarg_0), new(OpCodes.Ldloc_3), new(OpCodes.Ldloc_S, (byte)5) };
        for (int i = 0; i < sites; i++)
        {
            var ci = new CodeInstruction(OpCodes.Callvirt, call);
            if (label.HasValue && i == 0) ci.labels.Add(label.Value);
            list.Add(ci);
            list.Add(new CodeInstruction(OpCodes.Nop));
        }
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    [TestMethod]
    public void OneSite_LoadsTheMountItem_ThenCallsTheHook()
    {
        var vanilla = Vanilla();
        var logger = Substitute.For<IModLogger>();
        var input = Stream(vanilla, 1);

        var output = MountReinsTranspiler.SwapReinsCloth(input, logger).ToList();

        int hook = output.FindIndex(ci => ci.operand is MethodInfo mi && mi == MountReinsTranspiler.HookCall);
        Assert.AreEqual(4, hook, "the hook sits where the vanilla call was, after one inserted load");
        Assert.AreEqual(OpCodes.Call, output[hook].opcode, "the hook is static");
        Assert.AreEqual(OpCodes.Ldarg_1, output[hook - 1].opcode, "argument 1 of AddMountMesh is the mount item");
        Assert.IsFalse(output.Any(ci => ci.operand is MethodInfo mi && mi == vanilla), "no vanilla cloth call may remain");
        Assert.AreEqual(input.Count + 1, output.Count, "exactly one instruction is inserted");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void OneSite_MovesItsLabels_ToTheInsertedLoad()
    {
        var vanilla = Vanilla();
        var label = new Label();
        var output = MountReinsTranspiler.SwapReinsCloth(Stream(vanilla, 1, label), null).ToList();

        int hook = output.FindIndex(ci => ci.operand is MethodInfo mi && mi == MountReinsTranspiler.HookCall);
        CollectionAssert.AreEqual(new[] { label }, output[hook - 1].labels, "a branch to the call must also load the mount item");
        Assert.AreEqual(0, output[hook].labels.Count);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void AnyOtherSiteCount_ReturnsTheStreamUntouched_AndWarnsOnce(int sites)
    {
        var vanilla = Vanilla();
        var logger = Substitute.For<IModLogger>();
        var input = Stream(vanilla, sites);
        var before = input.Select(ci => (ci.opcode, ci.operand)).ToList();

        var output = MountReinsTranspiler.SwapReinsCloth(input, logger).ToList();

        CollectionAssert.AreEqual(before, output.Select(ci => (ci.opcode, ci.operand)).ToList());
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("[MountReins]")));
    }
}
