using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The Patch101 call-site swap in <c>CampaignEvents.Tick</c>, on a synthetic body shaped like the installed
/// one (<c>call get_Instance; ldfld _tickEvent; ldarg.1; callvirt MbEvent&lt;float&gt;::Invoke(float); ret</c>,
/// with <c>ldnull</c> standing in for the first two). Metadata reads only, so it also runs on the reference
/// assemblies; the real IL is <c>MapFrameProfilerBindingTests</c>.
/// </summary>
[TestClass]
public class TickEventSwapTests
{
    private static readonly MethodInfo Invoke = AccessTools.Method(typeof(MbEvent<float>), "Invoke", new[] { typeof(float) });

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static List<CodeInstruction> Body(int invokes)
    {
        var body = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldnull), new CodeInstruction(OpCodes.Ldarg_1) };
        for (var i = 0; i < invokes; i++)
            body.Add(new CodeInstruction(OpCodes.Callvirt, Invoke));
        body.Add(new CodeInstruction(OpCodes.Ret));
        return body;
    }

    [TestMethod]
    public void TickEventSwaps_SyntheticTickBody_SwapsTheOneInvoke()
    {
        var result = TickProfilerTranspiler.Rewrite(Body(1), MapProfilerTargets.TickEventSwaps(),
            "CampaignEvents.Tick", _logger, out var swapped);

        Assert.AreEqual(1, swapped);
        Assert.AreEqual(OpCodes.Call, result[2].opcode);
        Assert.AreEqual(AccessTools.Method(typeof(MapFrameProfilerHooks), nameof(MapFrameProfilerHooks.TimedTickEvent)), result[2].operand);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TickEventSwaps_HelperFitsTheTarget()
    {
        var swap = MapProfilerTargets.TickEventSwaps().Single();
        var helper = swap.Helpers.Single();

        Assert.AreEqual(Invoke, swap.Target);
        Assert.IsTrue(helper.IsStatic);
        Assert.AreEqual(typeof(void), helper.ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(MbEvent<float>), typeof(float) },
            helper.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    public void TickEventSwaps_InvokeTwice_LeavesTheBodyVanilla()
    {
        var result = TickProfilerTranspiler.Rewrite(Body(2), MapProfilerTargets.TickEventSwaps(),
            "CampaignEvents.Tick", _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        _logger.Received(1).LogWarning(Arg.Any<string>());
        CollectionAssert.AreEqual(Body(2).Select(i => i.opcode).ToArray(), result.Select(i => i.opcode).ToArray());
        Assert.IsTrue(result.Where(i => i.opcode == OpCodes.Callvirt).All(i => Equals(i.operand, Invoke)));
    }
}
