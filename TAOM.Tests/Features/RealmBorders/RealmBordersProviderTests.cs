using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The settings and palette are player-editable (MCM, a JSON file), so every parseable-but-wrong
/// value falls back to a safe default with a warning instead of reaching the fade or the mesh.
/// </summary>
[TestClass]
public class RealmBordersProviderTests
{
    [TestMethod]
    public void SaneFade_ValidPair_IsKept()
    {
        Assert.AreEqual((30f, 90f), RealmBordersSettingsProvider.SaneFade(30f, 90f));
    }

    [TestMethod]
    public void SaneFade_NaNStart_RevertsThePair()
    {
        var expected = (RealmBordersSettingsProvider.DefaultFadeStartDistance, RealmBordersSettingsProvider.DefaultFullOpacityDistance);

        Assert.AreEqual(expected, RealmBordersSettingsProvider.SaneFade(float.NaN, 90f));
    }

    [TestMethod]
    public void SaneFade_StartNotBelowFull_RevertsThePair()
    {
        var expected = (RealmBordersSettingsProvider.DefaultFadeStartDistance, RealmBordersSettingsProvider.DefaultFullOpacityDistance);

        Assert.AreEqual(expected, RealmBordersSettingsProvider.SaneFade(120f, 90f));
    }

    [TestMethod]
    public void SaneFade_InfiniteFull_RevertsThePair()
    {
        var expected = (RealmBordersSettingsProvider.DefaultFadeStartDistance, RealmBordersSettingsProvider.DefaultFullOpacityDistance);

        Assert.AreEqual(expected, RealmBordersSettingsProvider.SaneFade(10f, float.PositiveInfinity));
    }

    [TestMethod]
    public void Sane_OutOfRangeWidth_FallsBack()
    {
        Assert.AreEqual(1f, RealmBordersSettingsProvider.Sane(9f, 0.5f, 3f, 1f));
        Assert.AreEqual(1f, RealmBordersSettingsProvider.Sane(float.NaN, 0.5f, 3f, 1f));
        Assert.AreEqual(2f, RealmBordersSettingsProvider.Sane(2f, 0.5f, 3f, 1f));
    }

    [TestMethod]
    public void CheckedWidthScale_NotANumber_FallsBackAndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);

        Assert.AreEqual(1f, provider.CheckedWidthScale(float.NaN));
        Assert.AreEqual(1f, provider.CheckedWidthScale(float.NaN));

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Border Width")));
    }

    [TestMethod]
    public void CheckedWidthScale_BadAgainAfterAGoodValue_WarnsAgain()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);

        provider.CheckedWidthScale(9f);
        Assert.AreEqual(2f, provider.CheckedWidthScale(2f));
        provider.CheckedWidthScale(9f);

        logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void CheckedFade_StartNotBelowFull_RevertsBothAndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);

        Assert.AreEqual((45f, 110f), provider.CheckedFade(200f, 100f));
        provider.CheckedFade(200f, 100f);

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Fade In From Camera Distance")));
    }

    [TestMethod]
    public void SaneFade_OutsideZeroToFiveThousand_RevertsThePair()
    {
        Assert.AreEqual((45f, 110f), RealmBordersSettingsProvider.SaneFade(-1f, 90f));
        Assert.AreEqual((45f, 110f), RealmBordersSettingsProvider.SaneFade(10f, 5001f));
    }

    [TestMethod]
    public void Sane_WidthBelowHalf_FallsBack()
    {
        Assert.AreEqual(1f, RealmBordersSettingsProvider.Sane(0.4f, 0.5f, 3f, 1f));
        Assert.AreEqual(0.5f, RealmBordersSettingsProvider.Sane(0.5f, 0.5f, 3f, 1f), "the floor itself is allowed");
    }

    [TestMethod]
    public void CheckedFade_ValidPairOrNoSettings_StaysQuiet()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);

        Assert.AreEqual((30f, 90f), provider.CheckedFade(30f, 90f));
        Assert.AreEqual((45f, 110f), provider.CheckedFade(null, null));

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void BuildPalette_MalformedColour_IsSkippedWithAWarning()
    {
        var logger = Substitute.For<IModLogger>();
        var config = new RealmPaletteConfig
        {
            Realms = new Dictionary<string, string> { ["empire_w"] = "#3F76B8", ["empire_s"] = "red" },
            Reserve = new List<string> { "#2040D0", "nope" },
        };

        var palette = RealmPaletteProvider.Build(config, logger);

        Assert.AreEqual(0xFF3F76B8u, palette.ColourOf("empire_w"));
        Assert.AreEqual(0xFF2040D0u, palette.ColourOf("empire_s"), "the bad row takes the one good reserve colour");
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empire_s")));
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nope")));
    }

    [TestMethod]
    public void BuildPalette_NoConfig_StillColoursEveryRealm()
    {
        var palette = RealmPaletteProvider.Build(null, Substitute.For<IModLogger>());

        Assert.AreEqual(0xFF000000u, palette.ColourOf("empire_w") & 0xFF000000u);
    }

    // --- MCM: colours, blend, material ---

    private static string?[] Texts(params (string Realm, string Text)[] set)
    {
        var texts = new string?[RealmBordersSettingsProvider.ColourFields.Length];
        foreach (var (realm, text) in set)
            texts[Array.FindIndex(RealmBordersSettingsProvider.ColourFields, f => f.Realm == realm)] = text;
        return texts;
    }

    [TestMethod]
    public void RefreshColours_ValidColour_OverridesThePalette()
    {
        var provider = new RealmBordersSettingsProvider(Substitute.For<IModLogger>());

        provider.RefreshColours(Texts(("aserai", " #E8402A ")));

        Assert.AreEqual(0xFFE8402Au, provider.ColourOverride("aserai"));
        Assert.IsNull(provider.ColourOverride("empire_w"), "a blank field keeps the palette's colour");
    }

    [TestMethod]
    public void RefreshColours_MalformedColour_KeepsThePaletteAndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);

        provider.RefreshColours(Texts(("aserai", "red")));
        provider.RefreshColours(Texts(("aserai", "red")));

        Assert.IsNull(provider.ColourOverride("aserai"));
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Harad") && m.Contains("red")));
    }

    [TestMethod]
    public void RefreshColours_OnlyAChange_BumpsTheVersion()
    {
        var provider = new RealmBordersSettingsProvider(Substitute.For<IModLogger>());
        int first = provider.RefreshColours(Texts(("aserai", "#E8402A")));

        int same = provider.RefreshColours(Texts(("aserai", "#E8402A")));
        int changed = provider.RefreshColours(Texts(("aserai", "#E8402B")));

        Assert.AreEqual(first, same);
        Assert.AreNotEqual(same, changed);
    }

    [TestMethod]
    public void Choice_FirstEntryOrOutOfRange_IsTheDefault()
    {
        Assert.IsNull(RealmBordersSettingsProvider.Choice(0, RealmBordersSettingsProvider.BlendModeChoices));
        Assert.IsNull(RealmBordersSettingsProvider.Choice(null, RealmBordersSettingsProvider.BlendModeChoices));
        Assert.IsNull(RealmBordersSettingsProvider.Choice(99, RealmBordersSettingsProvider.BlendModeChoices));
        Assert.AreEqual("Modulate", RealmBordersSettingsProvider.Choice(2, RealmBordersSettingsProvider.BlendModeChoices));
    }

    [TestMethod]
    public void BlendModeChoices_KeepTheirSavedOrder_AndNameEveryEngineMode()
    {
        // MCM stores a dropdown's index: this order is what players' saved settings point into. A mode an
        // engine bump adds goes at the END, never between.
        CollectionAssert.AreEqual(new[]
        {
            "Material default", "NoAlphaBlend", "Modulate", "AddAlpha", "Multiply", "Add", "Max", "Factor", "AddModulateCombined",
            "NoAlphaBlendNoWrite", "ModulateNoWrite", "GbufferAlphaBlend", "GbufferAlphaBlendWithVtResolve", "NoAlphaBlendNoAlphaWrite",
        }, RealmBordersSettingsProvider.BlendModeChoices);

        var engine = Enum.GetNames(typeof(TaleWorlds.Engine.Material.MBAlphaBlendMode)).Where(n => n != "Total").ToArray();
        CollectionAssert.IsSubsetOf(engine, RealmBordersSettingsProvider.BlendModeChoices, "every engine blend mode has a dropdown entry");
        foreach (var name in RealmBordersSettingsProvider.BlendModeChoices.Skip(1))
            Assert.IsTrue(TAOM.Adapters.BorderRenderAdapter.TryParseBlendMode(name.ToLowerInvariant(), out _), name);
    }

    [TestMethod]
    public void TryParseBlendMode_OnlyAnExactName()
    {
        Assert.IsFalse(TAOM.Adapters.BorderRenderAdapter.TryParseBlendMode("2", out _), "a number is not a name");
        Assert.IsFalse(TAOM.Adapters.BorderRenderAdapter.TryParseBlendMode("Modulate,Add", out _));
        Assert.IsFalse(TAOM.Adapters.BorderRenderAdapter.TryParseBlendMode("Total", out _));
        Assert.IsFalse(TAOM.Adapters.BorderRenderAdapter.TryParseBlendMode(" ", out _));
    }

    [TestMethod]
    public void MaterialChoices_KeepTheirOrder_BecauseMcmStoresTheIndex()
    {
        CollectionAssert.AreEqual(
            new[] { "Automatic", "vertex_color_mat", "vertex_color_lighting", "vertex_color_blend_after_postfx_mat" },
            RealmBordersSettingsProvider.MaterialChoices, "append only: MCM keeps the selected index");
    }

    [TestMethod]
    public void AutomaticMaterials_PreferTheNormallyBlendedLatePass_FromTheSameChoices()
    {
        var automatic = TAOM.Adapters.BorderRenderAdapter.AutomaticMaterials;

        Assert.AreEqual("vertex_color_blend_after_postfx_mat", automatic[0], "it blends normally, so the dark ink shows");
        CollectionAssert.AreEquivalent(RealmBordersSettingsProvider.MaterialChoices.Skip(1).ToArray(), automatic);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void BorderFlags_EveryBorderMaterialDrawsInTheLatePass_SoTheParchmentNeverCoversIt(bool drawThroughTerrain)
    {
        var flags = TAOM.Adapters.BorderRenderAdapter.BorderFlags(drawThroughTerrain);

        Assert.AreEqual(0x20000000u, (uint)flags & 0x20000000u, "render_after_postfx");
        Assert.IsTrue(flags.HasFlag(TaleWorlds.Engine.MaterialFlags.NoModifyDepthBuffer));
        Assert.AreEqual(drawThroughTerrain, flags.HasFlag(TaleWorlds.Engine.MaterialFlags.NoDepthTest));
    }

    [TestMethod]
    public void NeedsSecondWinding_OnlyWhenTheMaterialCullsBackFaces()
    {
        Assert.IsFalse(TAOM.Adapters.BorderRenderAdapter.NeedsSecondWinding(
                TaleWorlds.Engine.MaterialFlags.TwoSided | TaleWorlds.Engine.MaterialFlags.NoDepthTest),
            "a two-sided material shows one winding from both sides; a second blends every pixel twice");
        Assert.IsTrue(TAOM.Adapters.BorderRenderAdapter.NeedsSecondWinding(TaleWorlds.Engine.MaterialFlags.NoDepthTest));
    }

    [TestMethod]
    public void SheetFlags_TurnStreamingOffAndDrawInTheLatePass()
    {
        Assert.AreEqual(0x20000200u, (uint)TAOM.Adapters.BorderRenderAdapter.SheetFlags);
    }

    [TestMethod]
    public void SheetRenderOrders_InkUnderPaperUnderTheBordersEngineDefault()
    {
        Assert.IsTrue(TAOM.Adapters.BorderRenderAdapter.SheetInkRenderOrder < TAOM.Adapters.BorderRenderAdapter.SheetPaperRenderOrder);
        Assert.IsTrue(TAOM.Adapters.BorderRenderAdapter.SheetPaperRenderOrder < TAOM.Adapters.BorderRenderAdapter.EngineDefaultRenderOrder);
    }

    [TestMethod]
    public void ParchmentMap_NoSettings_IsOn()
    {
        Assert.IsTrue(new RealmBordersSettingsProvider(Substitute.For<IModLogger>()).ParchmentMap);
    }

    [TestMethod]
    public void RefreshColours_YourRealm_IsReadLikeTheOthers()
    {
        var logger = Substitute.For<IModLogger>();
        var provider = new RealmBordersSettingsProvider(logger);
        var texts = new string?[RealmBordersSettingsProvider.YourRealmSlot + 1];
        int blank = provider.RefreshColours(texts);

        texts[RealmBordersSettingsProvider.YourRealmSlot] = "#12AB34";
        int set = provider.RefreshColours(texts);
        Assert.AreEqual(0xFF12AB34u, provider.YourRealmColour);
        Assert.AreNotEqual(blank, set, "an edit bumps the version, which repaints the map");

        texts[RealmBordersSettingsProvider.YourRealmSlot] = "green";
        provider.RefreshColours(texts);
        Assert.IsNull(provider.YourRealmColour);
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Your Realm")));
    }
}
