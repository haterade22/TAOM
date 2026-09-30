using System.Collections.Generic;
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
}
