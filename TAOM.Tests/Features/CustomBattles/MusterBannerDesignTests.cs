using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CustomBattles.Muster;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// One Custom Battle army's banner: the chosen kingdom's full banner stays exactly as authored until the player edits
/// it, each edit touches only its own layers, a faction change resets the edits, and the battle gets the exact code.
/// </summary>
[TestClass]
public class MusterBannerDesignTests
{
    // a background and three icon layers (an ornament pair and the main emblem), as the multilayer kingdom banners are
    private const string Kingdom =
        "11.100.75.4345.4345.764.764.1.0.0." +
        "521.172.100.62.62.630.618.0.0.268." +
        "521.172.100.62.62.899.618.0.1.88." +
        "24019.31.240.400.400.765.873.1.0.0";

    // a background and one emblem, as the vanilla culture banners are
    private const string Simple = "11.14.14.1528.1528.764.764.1.0.0.133.116.116.483.483.764.764.0.0.0";

    private static MusterBannerDesign For(string code)
    {
        var design = new MusterBannerDesign();
        design.Reset(code);
        return design;
    }

    private static string[] Layer(string code, int index) =>
        string.Join(".", new List<string>(code.Split('.')).GetRange(index * 10, 10)).Split('.');

    [TestMethod]
    public void Code_NoEdits_IsTheKingdomCodeExactly()
    {
        var design = For(Kingdom);

        Assert.AreEqual(Kingdom, design.Code);
        Assert.IsFalse(design.IsEdited);
    }

    [TestMethod]
    public void Code_EmblemPreviewsShown_StaysTheKingdomCode()
    {
        // opening the emblem chooser draws every emblem on the army's banner; that must not change the banner itself
        var design = For(Kingdom);

        _ = design.WithEmblem(133);
        _ = design.WithEmblem(140);

        Assert.AreEqual(Kingdom, design.Code);
        Assert.IsNull(design.Emblem);
    }

    [TestMethod]
    public void SetBackgroundColor_RecoloursTheBackgroundOnly()
    {
        var design = For(Kingdom);

        design.SetBackgroundColor(9);

        var code = design.Code;
        CollectionAssert.AreEqual("11.9.9.4345.4345.764.764.1.0.0".Split('.'), Layer(code, 0));
        for (int i = 1; i < 4; i++) CollectionAssert.AreEqual(Layer(Kingdom, i), Layer(code, i));
        Assert.IsTrue(design.IsEdited);
    }

    [TestMethod]
    public void SetEmblemColor_RecoloursEveryIconLayerAndKeepsTheBackground()
    {
        var design = For(Kingdom);

        design.SetEmblemColor(7);

        var code = design.Code;
        CollectionAssert.AreEqual(Layer(Kingdom, 0), Layer(code, 0));
        for (int i = 1; i < 4; i++)
        {
            Assert.AreEqual("7", Layer(code, i)[1]);
            Assert.AreEqual("7", Layer(code, i)[2]);
            Assert.AreEqual(Layer(Kingdom, i)[0], Layer(code, i)[0], "the layer's mesh stays");
        }
    }

    [TestMethod]
    public void SetEmblem_OneIconLayer_SwapsTheMeshAndKeepsItsPlace()
    {
        var design = For(Simple);

        design.SetEmblem(200);

        Assert.AreEqual("11.14.14.1528.1528.764.764.1.0.0.200.116.116.483.483.764.764.0.0.0", design.Code);
        Assert.AreEqual(200, design.Emblem);
    }

    [TestMethod]
    public void SetEmblem_MultilayerBanner_LeavesOneCentredEmblemInTheFirstIconColours()
    {
        // the engine's SetIconMeshId swaps layer 1 only, which on a kingdom banner is often an ornament
        var design = For(Kingdom);

        design.SetEmblem(200);

        Assert.AreEqual("11.100.75.4345.4345.764.764.1.0.0.200.172.100.512.512.764.764.0.0.0", design.Code);
    }

    [TestMethod]
    public void SetEmblem_ThenEmblemColour_ColoursTheNewEmblem()
    {
        var design = For(Kingdom);

        design.SetEmblem(200);
        design.SetEmblemColor(7);

        Assert.AreEqual("11.100.75.4345.4345.764.764.1.0.0.200.7.7.512.512.764.764.0.0.0", design.Code);
    }

    [TestMethod]
    public void SetColor_NotAPaletteColour_IsIgnored()
    {
        // BannerManager.GetColorId answers -1 for a colour outside the palette
        var design = For(Kingdom);

        design.SetBackgroundColor(-1);
        design.SetEmblemColor(-1);
        design.SetEmblem(-1);

        Assert.AreEqual(Kingdom, design.Code);
        Assert.IsFalse(design.IsEdited);
    }

    [TestMethod]
    public void Reset_AfterEdits_IsTheNewKingdomUntouched()
    {
        // a faction change: the side starts again from the new kingdom's banner
        var design = For(Simple);
        design.SetBackgroundColor(9);
        design.SetEmblem(200);

        design.Reset(Kingdom);

        Assert.AreEqual(Kingdom, design.Code);
        Assert.IsFalse(design.IsEdited);
        Assert.IsNull(design.Emblem);
    }

    [TestMethod]
    public void Edits_TwoSides_StayIndependent()
    {
        var player = For(Kingdom);
        var enemy = For(Kingdom);

        player.SetBackgroundColor(9);

        Assert.AreEqual(Kingdom, enemy.Code);
        Assert.AreNotEqual(Kingdom, player.Code);
    }

    [TestMethod]
    public void Code_SourceDoesNotParse_StaysTheSourceAfterEdits()
    {
        var design = For("not a banner");

        design.SetBackgroundColor(9);

        Assert.AreEqual("not a banner", design.Code);
    }

    [TestMethod]
    public void ExactCode_WholeLayers_ReturnsTheCode()
    {
        Assert.AreEqual(Kingdom, MusterBannerDesign.ExactCode(Kingdom));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("1.2.3")]
    [DataRow("11.14.14.1528.1528.764.764.1.0.0.133")]
    [DataRow("11.14.14.1528.x.764.764.1.0.0")]
    public void ExactCode_NotAWholeBanner_ReturnsNull(string code)
    {
        // the engine's own check passes "1.2.3" as a banner with no layers; the battle keeps the legacy colours then
        Assert.IsNull(MusterBannerDesign.ExactCode(code));
    }

    [TestMethod]
    public void KingdomCode_MappedCulture_ReturnsItsKingdomBanner()
    {
        var map = new Dictionary<string, string> { ["gondor"] = Kingdom };

        Assert.AreEqual(Kingdom, MusterBannerDesign.KingdomCode(map, "gondor"));
    }

    [TestMethod]
    public void KingdomCode_UnmappedOrBrokenCulture_ReturnsNull()
    {
        var map = new Dictionary<string, string> { ["gondor"] = "1.2.3" };

        Assert.IsNull(MusterBannerDesign.KingdomCode(map, "gondor"));
        Assert.IsNull(MusterBannerDesign.KingdomCode(map, "harad"));
        Assert.IsNull(MusterBannerDesign.KingdomCode(null, "gondor"));
        Assert.IsNull(MusterBannerDesign.KingdomCode(map, null));
    }
}
