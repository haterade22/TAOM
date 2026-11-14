using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The character page's misty valley (fs_bg_default, 2048 by 1152) is contained in the screen, centred,
/// never cropped and never enlarged past 1.25 physical pixels per source pixel. Every size here is in
/// physical pixels, the units of the parent's Size and of ScaledSuggestedWidth, so the UI scale cannot
/// change the result.
/// </summary>
[TestClass]
public class CharacterBackdropWidgetTests
{
    private const float SourceWidth = 2048f, SourceHeight = 1152f;

    [DataTestMethod]
    [DataRow(5120f, 1440f, 2560f, 1440f, DisplayName = "5120x1440 ultrawide: height-bound, 1280 dark each side")]
    [DataRow(2560f, 1440f, 2560f, 1440f, DisplayName = "2560x1440: exactly the cap")]
    [DataRow(1920f, 1080f, 1920f, 1080f, DisplayName = "1920x1080: shrunk to fit")]
    [DataRow(3840f, 2160f, 2560f, 1440f, DisplayName = "3840x2160: capped, dark on all four sides")]
    [DataRow(1920f, 1200f, 1920f, 1080f, DisplayName = "1920x1200: width-bound, letterboxed")]
    public void TryFit_Screen_ContainsTheWholeImageAtMostAtTheCap(
        float screenWidth, float screenHeight, float expectedWidth, float expectedHeight)
    {
        Assert.IsTrue(CharacterBackdropWidget.TryFit(screenWidth, screenHeight, SourceWidth, SourceHeight,
            CharacterBackdropWidget.MaxEnlargement, out float width, out float height));

        Assert.AreEqual(expectedWidth, width, 0.001f);
        Assert.AreEqual(expectedHeight, height, 0.001f);
        Assert.IsTrue(width <= screenWidth && height <= screenHeight, "no crop");
        Assert.AreEqual(SourceWidth / SourceHeight, width / height, 0.002f, "no stretch");
        Assert.IsTrue(width / SourceWidth <= 1.25f + 1e-4f, "no enlargement past 1.25");
    }

    [TestMethod]
    public void TryFit_Ultrawide_LeavesEqualDarkMarginsBesideTheImage()
    {
        CharacterBackdropWidget.TryFit(5120f, 1440f, SourceWidth, SourceHeight,
            CharacterBackdropWidget.MaxEnlargement, out float width, out _);

        Assert.AreEqual(1280f, (5120f - width) / 2f, 0.001f);
    }

    [TestMethod]
    public void MaxEnlargement_IsOnePointTwoFive()
    {
        Assert.AreEqual(1.25f, CharacterBackdropWidget.MaxEnlargement);
    }

    [DataTestMethod]
    [DataRow(1366f, 768f, DisplayName = "1366x768: a fractional fit")]
    [DataRow(1600f, 900f, DisplayName = "1600x900")]
    public void TryFit_FractionalScale_SnapsToWholePixels(float screenWidth, float screenHeight)
    {
        CharacterBackdropWidget.TryFit(screenWidth, screenHeight, SourceWidth, SourceHeight,
            CharacterBackdropWidget.MaxEnlargement, out float width, out float height);

        Assert.AreEqual(Math.Round(width), width, "whole pixels keep the image sharp");
        Assert.AreEqual(Math.Round(height), height);
        Assert.IsTrue(width <= screenWidth && height <= screenHeight);
    }

    // GauntletPreview at UI scale 0.797 laid out a 1920 by 1200 parent as 1919.99..., and the image lost a column.
    [TestMethod]
    public void TryFit_ContainerJustUnderAWholePixel_KeepsThatPixel()
    {
        CharacterBackdropWidget.TryFit(1919.996f, 1199.996f, SourceWidth, SourceHeight,
            CharacterBackdropWidget.MaxEnlargement, out float width, out float height);

        Assert.AreEqual(1920f, width);
        Assert.AreEqual(1080f, height);
    }

    [DataTestMethod]
    [DataRow(0f, 1440f, SourceWidth, SourceHeight, DisplayName = "no layout yet")]
    [DataRow(5120f, float.NaN, SourceWidth, SourceHeight, DisplayName = "NaN screen")]
    [DataRow(5120f, 1440f, 0f, SourceHeight, DisplayName = "no sprite")]
    [DataRow(5120f, 1440f, SourceWidth, float.NaN, DisplayName = "NaN sprite")]
    public void TryFit_InvalidSize_Declines(float screenWidth, float screenHeight, float sourceWidth, float sourceHeight)
    {
        Assert.IsFalse(CharacterBackdropWidget.TryFit(screenWidth, screenHeight, sourceWidth, sourceHeight,
            CharacterBackdropWidget.MaxEnlargement, out _, out _));
    }

    [DataTestMethod]
    [DataRow(2560f, 320f)]
    [DataRow(1920f, 240f)]
    public void FadeWidth_Image_IsAnEighthOfTheImageWidth(float imageWidth, float expected)
    {
        Assert.AreEqual(expected, CharacterBackdropWidget.FadeWidth(imageWidth), 0.001f);
    }

    // The top and bottom fades are the side ramps turned 90 degrees about their centre, so each is laid out
    // fade wide by image wide and shifted from the image centre by offset; it then covers the band from the
    // image edge inward.
    [DataTestMethod]
    [DataRow(2160f, 1440f, 180f, 630f, DisplayName = "3840x2160: capped image, 360 dark above and below")]
    [DataRow(1200f, 1080f, 135f, 472.5f, DisplayName = "1920x1200: width-bound, 60 dark above and below")]
    public void VerticalFade_Letterboxed_CoversAnEighthOfTheImageHeightFromEachEdge(
        float containerHeight, float imageHeight, float expectedFade, float expectedOffset)
    {
        Assert.IsTrue(CharacterBackdropWidget.VerticalFade(containerHeight, imageHeight, out float fade, out float offset));

        Assert.AreEqual(expectedFade, fade, 0.001f);
        Assert.AreEqual(expectedOffset, offset, 0.001f);
        Assert.AreEqual(0f, imageHeight / 2f - offset - fade / 2f, 0.001f, "the top band starts on the image's top edge");
    }

    [DataTestMethod]
    [DataRow(1440f, 1440f, DisplayName = "5120x1440 and 2560x1440: the image fills the height")]
    [DataRow(1080f, 1080f, DisplayName = "1920x1080")]
    [DataRow(1440.4f, 1440f, DisplayName = "a sub-pixel remainder is not a letterbox")]
    [DataRow(float.NaN, 1440f, DisplayName = "NaN container")]
    public void VerticalFade_ImageFillsTheHeight_IsHidden(float containerHeight, float imageHeight)
    {
        Assert.IsFalse(CharacterBackdropWidget.VerticalFade(containerHeight, imageHeight, out _, out _));
    }
}
