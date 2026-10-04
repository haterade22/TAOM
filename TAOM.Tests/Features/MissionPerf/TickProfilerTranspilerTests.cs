using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Matcher-level tests for the Patch97 call swaps, on synthetic streams shaped like the real IL
/// (<c>ldarg.0; call get_MissionBehaviors; ldloc; callvirt get_Item; ldarg.1; callvirt OnMissionTick</c>).
/// The <see cref="MethodInfo"/>s of <see cref="MissionBehavior"/> and <see cref="Mission"/> are metadata
/// reads, which work on the reference assemblies; nothing engine-side is invoked. The real helpers
/// against the real IL are <c>MissionTickProfilerBindingTests</c>.
/// </summary>
[TestClass]
public class TickProfilerTranspilerTests
{
    private static readonly MethodInfo OnMissionTick = typeof(MissionBehavior).GetMethod(nameof(MissionBehavior.OnMissionTick))!;
    private static readonly MethodInfo OnPreDisplay = typeof(MissionBehavior).GetMethod(nameof(MissionBehavior.OnPreDisplayMissionTick))!;
    private static readonly MethodInfo WaitTickCompletion =
        typeof(Mission).GetMethod("WaitTickCompletion", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo GetBehaviors = typeof(Mission).GetProperty(nameof(Mission.MissionBehaviors))!.GetGetMethod();
    private static readonly MethodInfo GetItem = typeof(List<MissionBehavior>).GetMethod("get_Item")!;

    private static void StubBehaviourTick(MissionBehavior behavior, float dt) { }
    private static void StubWait(Mission mission) { }
    private static int StubWrongReturn(MissionBehavior behavior, float dt) => 0;
    private void StubInstance(MissionBehavior behavior, float dt) { }

    private static MethodInfo Stub(string name) =>
        typeof(TickProfilerTranspilerTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static List<CodeInstruction> OnTickShape() => new List<CodeInstruction>
    {
        new CodeInstruction(OpCodes.Ldarg_0),                     // [0]
        new CodeInstruction(OpCodes.Call, GetBehaviors),          // [1]
        new CodeInstruction(OpCodes.Ldloc_0),                     // [2]
        new CodeInstruction(OpCodes.Callvirt, GetItem),           // [3]
        new CodeInstruction(OpCodes.Ldarg_1),                     // [4]
        new CodeInstruction(OpCodes.Callvirt, OnPreDisplay),      // [5] site 1
        new CodeInstruction(OpCodes.Ldarg_0),                     // [6]
        new CodeInstruction(OpCodes.Call, GetBehaviors),          // [7]
        new CodeInstruction(OpCodes.Ldloc_1),                     // [8]
        new CodeInstruction(OpCodes.Callvirt, GetItem),           // [9]
        new CodeInstruction(OpCodes.Ldarg_1),                     // [10]
        new CodeInstruction(OpCodes.Callvirt, OnMissionTick),     // [11] site 2
        new CodeInstruction(OpCodes.Ret),                         // [12]
    };

    private static List<CallSwap> OnTickSwaps() => new List<CallSwap>
    {
        new CallSwap(OnPreDisplay, Stub(nameof(StubBehaviourTick))),
        new CallSwap(OnMissionTick, Stub(nameof(StubBehaviourTick))),
    };

    [TestMethod]
    public void Rewrite_OnTickShape_SwapsBothBehaviourCalls_ToStaticCalls()
    {
        var result = TickProfilerTranspiler.Rewrite(OnTickShape(), OnTickSwaps(), "Mission.OnTick", _logger, out _);

        Assert.AreEqual(OpCodes.Call, result[5].opcode);
        Assert.AreSame(Stub(nameof(StubBehaviourTick)), result[5].operand);
        Assert.AreEqual(OpCodes.Call, result[11].opcode);
        Assert.AreSame(Stub(nameof(StubBehaviourTick)), result[11].operand);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_SwappedInstruction_KeepsItsLabels()
    {
        var stream = OnTickShape();
        var label = new Label();
        stream[11].labels.Add(label);
        var original = stream[11];

        var result = TickProfilerTranspiler.Rewrite(stream, OnTickSwaps(), "Mission.OnTick", _logger, out _);

        Assert.AreSame(original, result[11], "The swap mutates the instruction in place.");
        CollectionAssert.AreEqual(new[] { label }, result[11].labels);
    }

    [TestMethod]
    public void Rewrite_LeavesEveryOtherInstructionUntouched()
    {
        var before = OnTickShape();
        var result = TickProfilerTranspiler.Rewrite(OnTickShape(), OnTickSwaps(), "Mission.OnTick", _logger, out _);

        Assert.AreEqual(before.Count, result.Count);
        for (var i = 0; i < before.Count; i++)
        {
            if (i == 5 || i == 11)
                continue;
            Assert.AreEqual(before[i].opcode, result[i].opcode, $"opcode at {i}");
            Assert.AreEqual(before[i].operand, result[i].operand, $"operand at {i}");
        }
    }

    [TestMethod]
    public void Rewrite_SiteMissing_LeavesStreamUnmodifiedAndWarnsOnce()
    {
        var stream = OnTickShape();
        stream.RemoveAt(11);
        stream.RemoveAt(10);

        var result = TickProfilerTranspiler.Rewrite(stream, OnTickSwaps(), "Mission.OnTick", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(OpCodes.Callvirt, result[5].opcode, "The other site must stay vanilla too.");
        Assert.AreSame(OnPreDisplay, result[5].operand);
        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(TickProfileLines.BuildSiteCountWarning("Mission.OnTick", "MissionBehavior.OnMissionTick", 0));
    }

    [TestMethod]
    public void Rewrite_SiteTwice_LeavesStreamUnmodifiedAndWarnsOnce()
    {
        var stream = OnTickShape();
        stream.Insert(12, new CodeInstruction(OpCodes.Callvirt, OnMissionTick));

        var result = TickProfilerTranspiler.Rewrite(stream, OnTickSwaps(), "Mission.OnTick", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.IsTrue(result.All(ci => ci.opcode != OpCodes.Call || ci.operand is not MethodInfo m || m.DeclaringType != typeof(TickProfilerTranspilerTests)));
        _logger.Received(1).LogWarning(TickProfileLines.BuildSiteCountWarning("Mission.OnTick", "MissionBehavior.OnMissionTick", 2));
    }

    [TestMethod]
    public void Rewrite_HelperShapeMismatch_LeavesStreamUnmodifiedAndWarns()
    {
        var mismatches = new[]
        {
            new CallSwap(OnMissionTick, Stub(nameof(StubInstance))),        // not static
            new CallSwap(OnMissionTick, Stub(nameof(StubWait))),            // wrong parameters
            new CallSwap(OnMissionTick, Stub(nameof(StubWrongReturn))),     // wrong return type
        };
        foreach (var bad in mismatches)
        {
            var logger = Substitute.For<IModLogger>();
            var swaps = new List<CallSwap> { new CallSwap(OnPreDisplay, Stub(nameof(StubBehaviourTick))), bad };

            var result = TickProfilerTranspiler.Rewrite(OnTickShape(), swaps, "Mission.OnTick", logger, out var swapped);

            Assert.AreEqual(0, swapped, bad.Helper.Name);
            Assert.AreEqual(OpCodes.Callvirt, result[5].opcode, bad.Helper.Name);
            Assert.AreEqual(OpCodes.Callvirt, result[11].opcode, bad.Helper.Name);
            logger.Received(1).LogWarning(Arg.Is<string>(s => s.StartsWith("[TickProfiler] Mission.OnTick: helper ")));
        }
    }

    [TestMethod]
    public void Rewrite_RunTwiceOnItsOwnOutput_NeverThrows_AndLeavesItUnmodified()
    {
        var first = TickProfilerTranspiler.Rewrite(OnTickShape(), OnTickSwaps(), "Mission.OnTick", _logger, out _);
        var snapshot = first.Select(ci => (ci.opcode, ci.operand)).ToList();

        var second = TickProfilerTranspiler.Rewrite(first, OnTickSwaps(), "Mission.OnTick", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        CollectionAssert.AreEqual(snapshot, second.Select(ci => (ci.opcode, ci.operand)).ToList());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_ReportsSwappedCount()
    {
        TickProfilerTranspiler.Rewrite(OnTickShape(), OnTickSwaps(), "Mission.OnTick", _logger, out var two);
        Assert.AreEqual(2, two);

        var preTick = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, WaitTickCompletion),
            new CodeInstruction(OpCodes.Ret),
        };
        TickProfilerTranspiler.Rewrite(preTick, new List<CallSwap> { new CallSwap(WaitTickCompletion, Stub(nameof(StubWait))) },
            "Mission.OnPreTick", _logger, out var one);
        Assert.AreEqual(1, one);
        Assert.AreEqual(OpCodes.Call, preTick[1].opcode);
        Assert.AreSame(Stub(nameof(StubWait)), preTick[1].operand);
    }
}
