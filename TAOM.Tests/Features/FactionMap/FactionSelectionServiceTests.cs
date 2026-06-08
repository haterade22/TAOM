using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionSelectionServiceTests
{
    private IFactionRegistryService _registry;
    private FactionSelectionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<IFactionRegistryService>();
        _sut = new FactionSelectionService(_registry);
    }

    [TestMethod]
    public void SelectRegion_PlayableFactionWithCapital_ReturnsFullResult()
    {
        var region = new RegionData
        {
            FactionId = "kingdom_of_gondor",
            CapitalX = 0.47f,
            CapitalY = 0.57f,
        };
        var faction = new FactionData
        {
            Name = "Kingdom of Gondor",
            Color = "#4169E1FF",
            Playable = true,
            GameFaction = "gondor",
            Side = "free",
            Description = "The White City",
            Image = "faction_gondor",
            Traits = new[] { "Stalwart Defenders" },
            Bonuses = new[] { new FactionBonus { Text = "+10%", Positive = true } },
            SpecialUnits = new[] { new FactionSpecialUnit { Name = "Swan Knights", Description = "Elite" } },
            Perks = new[] { new FactionPerk { Name = "White Tower", Description = "+15%" } },
            Strengths = new[] { "Infantry" },
            Weaknesses = new[] { "Cavalry" },
            Difficulty = 2,
        };

        _registry.GetRegion("kingdom_of_gondor").Returns(region);
        _registry.GetFactionForRegion("kingdom_of_gondor").Returns(faction);

        var result = _sut.SelectRegion("kingdom_of_gondor");

        Assert.IsTrue(result.Found);
        Assert.AreEqual("Kingdom of Gondor", result.FactionName);
        Assert.IsTrue(result.Playable);
        Assert.IsTrue(result.HasCulture);
        Assert.AreEqual("gondor", result.CultureId);
        Assert.AreEqual("free", result.Side);
        Assert.AreEqual(2, result.Difficulty);
        Assert.AreEqual("faction_gondor", result.ImageId);
        Assert.AreEqual(0.47f, result.BannerPosX, 0.001f);
        Assert.AreEqual(0.57f, result.BannerPosY, 0.001f);
        Assert.AreEqual("#4169E1FF", result.BannerColorHex);
        Assert.AreEqual("banner_kingdom_of_gondor", result.BannerImage);
        Assert.AreEqual(1, result.Traits.Length);
        Assert.AreEqual(1, result.Bonuses.Length);
        Assert.AreEqual(1, result.SpecialUnits.Length);
        Assert.AreEqual("Swan Knights", result.SpecialUnits[0].Name);
    }

    [TestMethod]
    public void SelectRegion_NonPlayableFaction_HidesBanner()
    {
        var region = new RegionData { FactionId = "deco_region", CapitalX = 0.5f, CapitalY = 0.5f };
        var faction = new FactionData { Name = "Decoration", Playable = false, GameFaction = "" };

        _registry.GetRegion("deco_region").Returns(region);
        _registry.GetFactionForRegion("deco_region").Returns(faction);

        var result = _sut.SelectRegion("deco_region");

        Assert.IsTrue(result.Found);
        Assert.IsFalse(result.Playable);
        Assert.IsFalse(result.HasCulture);
        Assert.AreEqual(-1f, result.BannerPosX, 0.001f);
        // Empty BannerImage signals "no banner" to the widget; the load short-circuits via
        // IsNullOrEmpty(_bannerImage) in BannerWidget.TryLoadTexture instead of trying to
        // resolve a placeholder PNG that doesn't exist on disk.
        Assert.AreEqual("", result.BannerImage);
    }

    [TestMethod]
    public void SelectRegion_UnknownRegion_ReturnsNotFound()
    {
        _registry.GetRegion("unknown").Returns((RegionData?)null);
        _registry.GetFactionForRegion("unknown").Returns((FactionData?)null);

        var result = _sut.SelectRegion("unknown");

        Assert.IsTrue(result.Found);
        Assert.AreEqual("unknown", result.FactionName);
        Assert.IsFalse(result.Playable);
    }

    [TestMethod]
    public void SelectRegion_EmptyName_ReturnsNotFound()
    {
        var result = _sut.SelectRegion("");

        Assert.IsFalse(result.Found);
    }

    [TestMethod]
    public void SelectRegion_NullName_ReturnsNotFound()
    {
        var result = _sut.SelectRegion(null!);

        Assert.IsFalse(result.Found);
    }

    [TestMethod]
    public void GetCultureIdForRegion_ValidRegion_ReturnsCultureId()
    {
        var faction = new FactionData { GameFaction = "gondor" };
        _registry.GetFactionForRegion("kingdom_of_gondor").Returns(faction);

        var result = _sut.GetCultureIdForRegion("kingdom_of_gondor");

        Assert.AreEqual("gondor", result);
    }

    [TestMethod]
    public void GetCultureIdForRegion_NoFaction_ReturnsNull()
    {
        _registry.GetFactionForRegion("unknown").Returns((FactionData?)null);

        var result = _sut.GetCultureIdForRegion("unknown");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MakeDarkPanelHex_ValidColor_ReturnsDarkTint()
    {
        var result = _sut.MakeDarkPanelHex("#FF0000FF");

        Assert.IsTrue(result.StartsWith("#"));
        Assert.AreEqual(9, result.Length);
        Assert.IsTrue(result.EndsWith("D0"));
    }

    [TestMethod]
    public void MakeDarkPanelHex_EmptyColor_ReturnsDefault()
    {
        var result = _sut.MakeDarkPanelHex("");

        Assert.AreEqual("#1a1a2eFF", result);
    }

    [TestMethod]
    public void MakeAccentColorHex_ValidColor_ReturnsDarkenedColor()
    {
        var result = _sut.MakeAccentColorHex("#FF8800FF");

        Assert.IsTrue(result.StartsWith("#"));
        Assert.AreEqual(9, result.Length);
        Assert.IsTrue(result.EndsWith("FF"));
    }

    [TestMethod]
    public void MakeAccentColorHex_EmptyColor_ReturnsDefault()
    {
        var result = _sut.MakeAccentColorHex("");

        Assert.AreEqual("#835513FF", result);
    }

    [TestMethod]
    public void FormatDifficultyText_ValidDifficulty_ReturnsKeyedString()
    {
        // Strings are wrapped in {=KEY}default so FactionDisplayHelper.Localize
        // can resolve them via TextObject — matches the localization sweep for
        // the rest of the CC faction-map page (#260 Phase 2).
        Assert.AreEqual("{=taom_faction_difficulty_1}Difficulty: Very Easy", _sut.FormatDifficultyText(1));
        Assert.AreEqual("{=taom_faction_difficulty_2}Difficulty: Easy", _sut.FormatDifficultyText(2));
        Assert.AreEqual("{=taom_faction_difficulty_3}Difficulty: Medium", _sut.FormatDifficultyText(3));
        Assert.AreEqual("{=taom_faction_difficulty_4}Difficulty: Medium-Hard", _sut.FormatDifficultyText(4));
        Assert.AreEqual("{=taom_faction_difficulty_5}Difficulty: Hard", _sut.FormatDifficultyText(5));
        Assert.AreEqual("{=taom_faction_difficulty_6}Difficulty: Very Hard", _sut.FormatDifficultyText(6));
        Assert.AreEqual("{=taom_faction_difficulty_7}Difficulty: Extreme", _sut.FormatDifficultyText(7));
    }

    [TestMethod]
    public void FormatDifficultyText_ZeroDifficulty_ReturnsEmpty()
    {
        Assert.AreEqual("", _sut.FormatDifficultyText(0));
    }

    [TestMethod]
    public void MakeDarkPanelHex_BlackColor_BlendsProperly()
    {
        var result = _sut.MakeDarkPanelHex("#000000FF");

        // With black input (r=0,g=0,b=0), output is pure base color:
        // R = (0.82 * 0.07 + 0.18 * 0) * 255 = 14.63 => 0E
        // G = (0.82 * 0.07 + 0.18 * 0) * 255 = 14.63 => 0E
        // B = (0.82 * 0.10 + 0.18 * 0) * 255 = 20.91 => 14
        Assert.AreEqual("#0E0E14D0", result);
    }

    [TestMethod]
    public void MakeAccentColorHex_BrightColor_DarkensCorrectly()
    {
        var result = _sut.MakeAccentColorHex("#FFFFFFFF");

        // With white input (r=1,g=1,b=1):
        // R = clamp(1.0 * 0.55 * 255) = 140 => 8C
        // G = clamp(1.0 * 0.55 * 255) = 140 => 8C
        // B = clamp(1.0 * 0.55 * 255) = 140 => 8C
        Assert.AreEqual("#8C8C8CFF", result);
    }

    [TestMethod]
    public void FormatDifficultyText_OutOfRange_ReturnsEmpty()
    {
        Assert.AreEqual("", _sut.FormatDifficultyText(8));
        Assert.AreEqual("", _sut.FormatDifficultyText(100));
        Assert.AreEqual("", _sut.FormatDifficultyText(-1));
    }
}
