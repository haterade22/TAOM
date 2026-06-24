using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.PartyIconScale;

namespace TAOM.Tests.Features.PartyIconScale;

[TestClass]
public class PartyIconScaleTranspilerTests
{
    private static readonly MethodInfo GetScale =
        AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

    // Stand-in for AgentVisualsData.Scale as the Callvirt operand. The transpiler matches on the
    // method NAME only ("Scale"), so any MethodInfo named "Scale" reproduces the IL shape.
    private static float Scale(float x) => x;

    private static readonly MethodInfo ScaleStub =
        typeof(PartyIconScaleTranspilerTests).GetMethod(nameof(Scale),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    [TestMethod]
    public void Rewrite_PeopleSite_SwapsLdcForGetScaleCall()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Callvirt, ScaleStub),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        Assert.AreSame(GetScale, result[0].operand);
        Assert.AreEqual(OpCodes.Callvirt, result[1].opcode); // Scale call left intact
    }

    [TestMethod]
    public void Rewrite_MountSite_SwapsLdcForGetScaleCall()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Mul),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        Assert.AreSame(GetScale, result[0].operand);
        Assert.AreEqual(OpCodes.Mul, result[1].opcode); // multiply (ScaleFactor * scale) left intact
    }

    [TestMethod]
    public void Rewrite_BothSitesWithDecoys_SwapsExactlyTheTwoZeroPointThree()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.325f),      // [0] decoy: wrong literal, before Mul
            new CodeInstruction(OpCodes.Mul),                 // [1]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [2] PEOPLE: 0.3 before Callvirt Scale
            new CodeInstruction(OpCodes.Callvirt, ScaleStub), // [3]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [4] decoy: 0.3 before Div (anim math)
            new CodeInstruction(OpCodes.Div),                 // [5]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [6] MOUNT: 0.3 before Mul
            new CodeInstruction(OpCodes.Mul),                 // [7]
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[2].opcode, "people 0.3 should swap to GetScale call");
        Assert.AreEqual(OpCodes.Call, result[6].opcode, "mount 0.3 should swap to GetScale call");
        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode, "0.325 decoy untouched");
        Assert.AreEqual(OpCodes.Ldc_R4, result[4].opcode, "0.3-before-Div decoy untouched");
    }

    [TestMethod]
    public void Rewrite_PreservesLabelsOnSwappedInstruction()
    {
        var label = new Label();
        var ldc = new CodeInstruction(OpCodes.Ldc_R4, 0.3f);
        ldc.labels.Add(label);
        var input = new List<CodeInstruction>
        {
            ldc,
            new CodeInstruction(OpCodes.Mul),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        CollectionAssert.Contains(result[0].labels, label);
    }

    [TestMethod]
    public void Rewrite_NullGetScale_ReturnsUnchangedAndWarns()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Mul),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, null, _logger);

        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode); // untouched — vanilla 0.3 preserved
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_PeopleSiteMissing_WarnsButStillSwapsMount()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f), // mount-only stream
            new CodeInstruction(OpCodes.Mul),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode); // mount still swapped
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("people")));
    }

    [TestMethod]
    public void Rewrite_AppliedTwice_SecondPassIsNoOpAndDoesNotThrow()
    {
        // Pins the doc-comment promise that a category re-apply "can't crash": after the first pass the
        // ldc.r4 0.3 sites are Call instructions, so the second pass finds nothing to swap, warns, and
        // returns the stream unchanged — never throws or double-rewrites.
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // people
            new CodeInstruction(OpCodes.Callvirt, ScaleStub),
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // mount
            new CodeInstruction(OpCodes.Mul),
        };

        var first = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
        Assert.AreEqual(OpCodes.Call, first[0].opcode);
        Assert.AreEqual(OpCodes.Call, first[2].opcode);

        var second = PartyIconScaleTranspiler.Rewrite(first, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, second[0].opcode);
        Assert.AreEqual(OpCodes.Call, second[2].opcode);
        Assert.AreSame(GetScale, second[0].operand);
        Assert.AreSame(GetScale, second[2].operand);
    }

    [TestMethod]
    public void Rewrite_PeopleSiteUsesCallNotCallvirt_StillSwaps()
    {
        // NextIsScaleCall accepts OpCodes.Call as well as Callvirt — cover the Call branch.
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Call, ScaleStub),
        };

        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        Assert.AreSame(GetScale, result[0].operand);
    }
}
