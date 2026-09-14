using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.PartyIconScale;

namespace TAOM.Tests.Features.PartyIconScale;

/// <summary>
/// Matcher-level tests for the Patch53 IL surgery, driven by synthetic instruction streams.
///
/// <para>
/// These prove the MATCHER is correct. They cannot prove the sites still exist in the shipped
/// engine, because they never touch it. That is deliberately covered elsewhere, by
/// <c>TAOM.Tests/Migration/TranspilerSiteBindingTests.cs</c>, which feeds real engine IL through
/// these same helpers. Keep both: the v1.5.0 bump moved a site while every synthetic test here
/// stayed green.
/// </para>
///
/// <para>
/// v1.5.0 split the three scale sites across two methods, so there are two entry points.
/// <c>RewriteIconSites</c> runs against <c>MobilePartyVisual.AddCharacterToPartyIcon</c> and owns
/// the MOUNT site (0.3 before <c>mul</c>) plus the human world FRAME site (0.3 before
/// <c>ApplyScaleLocal</c>, newly hardcoded in v1.5.0). <c>RewriteHumanVisualSite</c> runs against
/// <c>MobilePartyVisualHelper.GetHumanAgentPartyVisual</c> and owns the PEOPLE site (0.3 before
/// <c>Scale</c>), which v1.5.0 relocated out of the icon method entirely.
/// </para>
/// </summary>
[TestClass]
public class PartyIconScaleTranspilerTests
{
    private static readonly MethodInfo GetScale =
        AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

    // Stand-ins for the engine methods used as call operands. The transpiler matches on the method
    // NAME only, so any MethodInfo with the right name reproduces the IL shape.
    private static float Scale(float x) => x;
    private static void ApplyScaleLocal(float x) { }

    private static readonly MethodInfo ScaleStub =
        typeof(PartyIconScaleTranspilerTests).GetMethod(nameof(Scale),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ApplyScaleLocalStub =
        typeof(PartyIconScaleTranspilerTests).GetMethod(nameof(ApplyScaleLocal),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    // A stream shaped like v1.5.0's AddCharacterToPartyIcon: mount site, then human frame site.
    private static List<CodeInstruction> IconStream() => new List<CodeInstruction>
    {
        new CodeInstruction(OpCodes.Ldc_R4, 0.3f),                  // [0] MOUNT
        new CodeInstruction(OpCodes.Mul),                           // [1]
        new CodeInstruction(OpCodes.Ldc_R4, 0.3f),                  // [2] HUMAN FRAME
        new CodeInstruction(OpCodes.Call, ApplyScaleLocalStub),     // [3]
    };

    // ---- RewriteHumanVisualSite: the relocated PEOPLE site -------------------------------------

    [TestMethod]
    public void HumanVisualSite_SwapsLdcForGetScaleCall()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Callvirt, ScaleStub),
        };

        var result = PartyIconScaleTranspiler.RewriteHumanVisualSite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        Assert.AreSame(GetScale, result[0].operand);
        Assert.AreEqual(OpCodes.Callvirt, result[1].opcode); // Scale call left intact
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void HumanVisualSite_UsesCallNotCallvirt_StillSwaps()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Call, ScaleStub),
        };

        var result = PartyIconScaleTranspiler.RewriteHumanVisualSite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        Assert.AreSame(GetScale, result[0].operand);
    }

    [TestMethod]
    public void HumanVisualSite_Missing_WarnsAndLeavesStreamUnchanged()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Div), // animation math, not a scale site
        };

        var result = PartyIconScaleTranspiler.RewriteHumanVisualSite(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("people")));
    }

    // ---- RewriteIconSites: MOUNT + the new human world FRAME ------------------------------------

    [TestMethod]
    public void IconSites_SwapsBothMountAndHumanFrame()
    {
        var result = PartyIconScaleTranspiler.RewriteIconSites(IconStream(), GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode, "mount 0.3 should swap to GetScale call");
        Assert.AreSame(GetScale, result[0].operand);
        Assert.AreEqual(OpCodes.Mul, result[1].opcode);
        Assert.AreEqual(OpCodes.Call, result[2].opcode, "human frame 0.3 should swap to GetScale call");
        Assert.AreSame(GetScale, result[2].operand);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void IconSites_WithDecoys_SwapsExactlyTheTwoRealSites()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.325f),               // [0] decoy: wrong literal
            new CodeInstruction(OpCodes.Mul),                          // [1]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),                 // [2] decoy: 0.3 before Div
            new CodeInstruction(OpCodes.Div),                          // [3]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),                 // [4] MOUNT
            new CodeInstruction(OpCodes.Mul),                          // [5]
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),                 // [6] HUMAN FRAME
            new CodeInstruction(OpCodes.Call, ApplyScaleLocalStub),    // [7]
        };

        var result = PartyIconScaleTranspiler.RewriteIconSites(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[4].opcode, "mount 0.3 should swap");
        Assert.AreEqual(OpCodes.Call, result[6].opcode, "human frame 0.3 should swap");
        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode, "0.325 decoy untouched");
        Assert.AreEqual(OpCodes.Ldc_R4, result[2].opcode, "0.3-before-Div decoy untouched");
    }

    [TestMethod]
    public void IconSites_MountPresentButFrameMissing_SwapsMountAndWarns()
    {
        var input = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
            new CodeInstruction(OpCodes.Mul),
        };

        var result = PartyIconScaleTranspiler.RewriteIconSites(input, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode, "mount still swapped");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("human frame")));
    }

    [TestMethod]
    public void IconSites_PreservesLabelsOnSwappedInstruction()
    {
        var label = new Label();
        var stream = IconStream();
        stream[0].labels.Add(label);

        var result = PartyIconScaleTranspiler.RewriteIconSites(stream, GetScale, _logger);

        Assert.AreEqual(OpCodes.Call, result[0].opcode);
        CollectionAssert.Contains(result[0].labels, label);
    }

    [TestMethod]
    public void IconSites_NullGetScale_ReturnsUnchangedAndWarns()
    {
        var result = PartyIconScaleTranspiler.RewriteIconSites(IconStream(), null, _logger);

        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode); // untouched, vanilla 0.3 preserved
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("GetScale lookup failed")));
    }

    [TestMethod]
    public void IconSites_AppliedTwice_SecondPassIsNoOpAndDoesNotThrow()
    {
        // Pins the doc-comment promise that a category re-apply cannot crash: after the first pass
        // the ldc.r4 0.3 sites are Call instructions, so the second pass finds nothing to swap,
        // warns, and returns the stream unchanged rather than throwing or double-rewriting.
        var first = PartyIconScaleTranspiler.RewriteIconSites(IconStream(), GetScale, _logger);
        Assert.AreEqual(OpCodes.Call, first[0].opcode);

        var second = PartyIconScaleTranspiler.RewriteIconSites(first, GetScale, _logger);

        Assert.AreEqual(first.Count, second.Count);
        Assert.AreEqual(OpCodes.Call, second[0].opcode);
        _logger.Received().LogWarning(Arg.Any<string>());
    }
}
