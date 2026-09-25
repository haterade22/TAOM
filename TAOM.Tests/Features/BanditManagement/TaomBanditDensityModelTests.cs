using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BanditManagement.Models;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// Cap()/Scale() are internal static (InternalsVisibleTo "TAOM.Tests") — they hold the only
/// computation in the otherwise-thin <see cref="TaomBanditDensityModel"/>, so they're unit-tested
/// directly. The model's property bodies (which read Campaign.Current.PlayerProgress) are entry
/// points and not unit-tested per ADR-008.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomBanditDensityModelTests
{
    [TestMethod]
    public void Cap_HardCapBelowVanillaBase_ReturnsVanillaBase_NotHardCap()
    {
        // Regression for the MED review finding: dragging the MCM cap below the vanilla base must
        // NOT drop density under vanilla. base=3 (vanilla parties/hideout), cap=2 (slider one notch
        // below the default of 3). "Vanilla is the floor" => result must be 3, not 2.
        Assert.AreEqual(3, TaomBanditDensityModel.Cap(3, 1.0f, 2));
    }

    [TestMethod]
    public void Cap_HardCapBelowVanillaBase_HighMultiplier_StillFloorsAtBase()
    {
        // base=9 (vanilla hideouts/faction), cap=4 < base, any multiplier => floor at 9.
        Assert.AreEqual(9, TaomBanditDensityModel.Cap(9, 2.5f, 4));
    }

    [TestMethod]
    public void Cap_MultiplierOne_ReturnsVanillaBase()
    {
        // PlayerProgress 0 => multiplier 1.0 => vanilla density.
        Assert.AreEqual(9, TaomBanditDensityModel.Cap(9, 1.0f, 100));
    }

    [TestMethod]
    public void Cap_ScaledExceedsHardCap_ReturnsHardCap()
    {
        // base=9, mult=2.5 => ~23, cap=15 => clamp to 15.
        Assert.AreEqual(15, TaomBanditDensityModel.Cap(9, 2.5f, 15));
    }

    [TestMethod]
    public void Cap_ScaledWithinRange_ReturnsScaled()
    {
        // base=9, mult=2.0 => 18, cap=100 => 18 (cap not binding).
        Assert.AreEqual(18, TaomBanditDensityModel.Cap(9, 2.0f, 100));
    }

    [TestMethod]
    public void Cap_ShippedPartiesPerHideoutCap_LetsTheDensityCurveMoveTheValue()
    {
        // #559: the cap shipped at 3, equal to vanilla's base, and Cap() takes max(base, cap) as the
        // ceiling, so parties-per-hideout was pinned at 3 for every PlayerProgress and Density Curve
        // did nothing there. Pin the SHIPPED default, not a literal, so a future default of 3 fails.
        var shippedCap = new TaomSettings().BanditMaxPartiesPerHideout;
        var endgame = TaomBanditDensityModel.Cap(3, 2.5f, shippedCap);
        Assert.IsTrue(endgame > 3, $"endgame parties/hideout is {endgame}: the shipped cap {shippedCap} pins it at vanilla");
        Assert.AreEqual(shippedCap, endgame, "the 1.5 curve reaches 2.5x at endgame, which the cap should bind");
    }

    [TestMethod]
    public void Cap_ShippedPartiesPerHideoutCap_StillFloorsAtVanillaOnDayOne()
    {
        // PlayerProgress 0 => multiplier 1.0 => vanilla 3, whatever the cap is.
        Assert.AreEqual(3, TaomBanditDensityModel.Cap(3, 1.0f, new TaomSettings().BanditMaxPartiesPerHideout));
    }

    [TestMethod]
    public void Scale_MultiplierOne_ReturnsVanillaBase()
    {
        Assert.AreEqual(22, TaomBanditDensityModel.Scale(22, 1.0f));
    }

    [TestMethod]
    public void Scale_HigherMultiplier_ScalesUp()
    {
        Assert.AreEqual(44, TaomBanditDensityModel.Scale(22, 2.0f));
    }
}
