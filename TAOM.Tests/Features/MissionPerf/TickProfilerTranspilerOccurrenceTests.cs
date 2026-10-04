using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The occurrence swaps plan 041 adds to the Patch97 rewrite: a target that occurs N times takes N helpers
/// in stream order (the four <c>TWParallel.For</c> blocks of <c>ManagedScriptHolder.TickComponents</c>), and
/// a static target's helper has exactly the target's parameters. Synthetic streams; the
/// <see cref="MethodInfo"/>s are metadata reads that work on the reference assemblies.
/// </summary>
[TestClass]
public class TickProfilerTranspilerOccurrenceTests
{
    private static readonly MethodInfo For = typeof(TWParallel).GetMethod(nameof(TWParallel.For),
        new[] { typeof(int), typeof(int), typeof(float), typeof(TWParallel.ParallelForWithDtAuxPredicate), typeof(int) })!;
    private static readonly MethodInfo ScriptOnTick = typeof(ScriptComponentBehavior).GetMethod("OnTick",
        BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float) }, null)!;
    private static readonly MethodInfo OnMissionTick = typeof(MissionBehavior).GetMethod(nameof(MissionBehavior.OnMissionTick))!;

    private static void StubBlockA(int fromInclusive, int toExclusive, float deltaTime, TWParallel.ParallelForWithDtAuxPredicate body, int grainSize) { }
    private static void StubBlockB(int fromInclusive, int toExclusive, float deltaTime, TWParallel.ParallelForWithDtAuxPredicate body, int grainSize) { }
    private static void StubBlockLeading(object extra, int fromInclusive, int toExclusive, float deltaTime, TWParallel.ParallelForWithDtAuxPredicate body, int grainSize) { }
    private static void StubScriptTick(ScriptComponentBehavior component, float dt) { }
    private static void StubBehaviourTick(MissionBehavior behavior, float dt) { }

    private static MethodInfo Stub(string name) =>
        typeof(TickProfilerTranspilerOccurrenceTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static List<CodeInstruction> Stream(int forCount, bool withScriptTick = false)
    {
        var list = new List<CodeInstruction> { new CodeInstruction(OpCodes.Nop) };
        for (var i = 0; i < forCount; i++)
        {
            list.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
            list.Add(new CodeInstruction(OpCodes.Call, For));
            if (withScriptTick && i == 2)
            {
                list.Add(new CodeInstruction(OpCodes.Ldarg_1));
                list.Add(new CodeInstruction(OpCodes.Callvirt, ScriptOnTick));
            }
        }
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    private static CallSwap ForSwap(params string[] helpers) => new CallSwap(For, helpers.Select(Stub).ToList());

    private static int[] ForSites(List<CodeInstruction> stream) => Enumerable.Range(0, stream.Count)
        .Where(i => ReferenceEquals(stream[i].operand, For) || (stream[i].operand is MethodInfo m && m.Name.StartsWith("StubBlock")))
        .ToArray();

    [TestMethod]
    public void Rewrite_FourOccurrencesWithFourHelpers_SwapsEachInOrder()
    {
        var stream = Stream(4);
        var sites = ForSites(stream);

        var result = TickProfilerTranspiler.Rewrite(stream, new[] { ForSwap(nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockB)) },
            "ManagedScriptHolder.TickComponents", _logger, out var swapped);

        Assert.AreEqual(4, swapped);
        Assert.AreSame(Stub(nameof(StubBlockA)), result[sites[0]].operand);
        Assert.AreSame(Stub(nameof(StubBlockA)), result[sites[1]].operand);
        Assert.AreSame(Stub(nameof(StubBlockA)), result[sites[2]].operand);
        Assert.AreSame(Stub(nameof(StubBlockB)), result[sites[3]].operand);
        Assert.IsTrue(sites.All(i => result[i].opcode == OpCodes.Call));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_OccurrenceCountDiffersFromHelperCount_LeavesStreamUnmodifiedAndWarnsOnce()
    {
        var result = TickProfilerTranspiler.Rewrite(Stream(3),
            new[] { ForSwap(nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockB)) },
            "ManagedScriptHolder.TickComponents", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.IsTrue(result.All(ci => !(ci.operand is MethodInfo m) || m == For), "No site may be swapped.");
        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(TickProfileLines.BuildSiteCountWarning("ManagedScriptHolder.TickComponents", "TWParallel.For", 3, 4));
    }

    [TestMethod]
    public void Rewrite_StaticTarget_HelperWithTheSameParameters_Validates()
    {
        var result = TickProfilerTranspiler.Rewrite(Stream(1), new[] { ForSwap(nameof(StubBlockA)) },
            "Test.Method", _logger, out var swapped);

        Assert.AreEqual(1, swapped);
        Assert.AreSame(Stub(nameof(StubBlockA)), result[2].operand);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_StaticTarget_HelperWithAnExtraLeadingParameter_IsRejected()
    {
        var result = TickProfilerTranspiler.Rewrite(Stream(1), new[] { ForSwap(nameof(StubBlockLeading)) },
            "Test.Method", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreSame(For, result[2].operand);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("does not fit TWParallel.For")));
    }

    [TestMethod]
    public void Rewrite_InstanceAndStaticSwapsTogether_SwapsAll()
    {
        var stream = Stream(4, withScriptTick: true);
        var scriptSite = stream.FindIndex(ci => ReferenceEquals(ci.operand, ScriptOnTick));

        var result = TickProfilerTranspiler.Rewrite(stream, new[]
            {
                ForSwap(nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockA), nameof(StubBlockB)),
                new CallSwap(ScriptOnTick, Stub(nameof(StubScriptTick))),
            },
            "ManagedScriptHolder.TickComponents", _logger, out var swapped);

        Assert.AreEqual(5, swapped);
        Assert.AreEqual(OpCodes.Call, result[scriptSite].opcode);
        Assert.AreSame(Stub(nameof(StubScriptTick)), result[scriptSite].operand);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_SingleHelperSwap_StillExpectsExactlyOne()
    {
        var stream = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Callvirt, OnMissionTick),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Callvirt, OnMissionTick),
            new CodeInstruction(OpCodes.Ret),
        };

        var result = TickProfilerTranspiler.Rewrite(stream, new[] { new CallSwap(OnMissionTick, Stub(nameof(StubBehaviourTick))) },
            "Mission.OnTick", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreSame(OnMissionTick, result[1].operand);
        Assert.AreSame(OnMissionTick, result[3].operand);
        _logger.Received(1).LogWarning(TickProfileLines.BuildSiteCountWarning("Mission.OnTick", "MissionBehavior.OnMissionTick", 2));
    }
}
