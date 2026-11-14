using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The encyclopedia bar and the page under it are separate movies, so they share one coordinate reference: the
/// 1728 by 972 frame. Every height here is logical (physical pixels times the inverse UI scale). The frame is
/// centred, as the bar always was, but never so low that the page navigation leaves its 22 margin, and never
/// above the viewport top. Issue #1212: pages at a fixed y 184 rose over the header above 1080.
/// </summary>
[TestClass]
public class EncyclopediaFrameAnchorWidgetTests
{
    private const float HeaderTop = 12f, HeaderBottom = 72f;
    private const float SearchBoxBottom = 60f;

    [DataTestMethod]
    [DataRow(900f, 0f, DisplayName = "900 (custom UI scale): pinned to the top")]
    [DataRow(1026f, 0f, DisplayName = "1026: the frame and its navigation just fit")]
    [DataRow(1050f, 24f, DisplayName = "1050: raised to keep the navigation margin")]
    [DataRow(1080f, 54f, DisplayName = "1080 (16:9, 21:9, 32:9): the shipped layout")]
    [DataRow(1176f, 102f, DisplayName = "1176 (16:10): centred")]
    [DataRow(1280f, 154f, DisplayName = "1280: centred")]
    [DataRow(1411f, 219.5f, DisplayName = "1411 (4:3): centred")]
    [DataRow(1440f, 234f, DisplayName = "1440: centred")]
    public void FrameTop_LogicalHeight_CentresWithinTheNavigationMargin(float height, float expected)
    {
        Assert.AreEqual(expected, EncyclopediaFrameAnchorWidget.FrameTop(height), 0.001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.PositiveInfinity)]
    public void FrameTop_InvalidHeight_KeepsTheShippedLayout(float height)
    {
        Assert.AreEqual(54f, EncyclopediaFrameAnchorWidget.FrameTop(height), 0.001f);
    }

    [TestMethod]
    public void Offset_At1080_MatchesTheShippedCoordinates()
    {
        // The page canvas sat at 184 and the navigation at 1010 (bottom 22); both prefabs keep these as defaults.
        Assert.AreEqual(184f, EncyclopediaFrameAnchorWidget.Offset(1080f, EncyclopediaFrameAnchorWidget.PageTop, 780f, EncyclopediaFrameAnchorWidget.PageClearance), 0.001f);
        Assert.AreEqual(1010f, EncyclopediaFrameAnchorWidget.Offset(1080f, EncyclopediaFrameAnchorWidget.NavigationTop, 48f, 22f), 0.001f);
    }

    [DataTestMethod]
    [DataRow(1026f)]
    [DataRow(1050f)]
    [DataRow(1080f)]
    [DataRow(1176f)]
    [DataRow(1280f)]
    [DataRow(1411f)]
    [DataRow(1440f)]
    [DataRow(2160f)]
    public void Offsets_FullHeightViewport_KeepTheHeaderClearAndEverythingOnScreen(float height)
    {
        float frame = EncyclopediaFrameAnchorWidget.FrameTop(height);
        float page = EncyclopediaFrameAnchorWidget.Offset(height, EncyclopediaFrameAnchorWidget.PageTop, 780f, EncyclopediaFrameAnchorWidget.PageClearance);
        float navigation = EncyclopediaFrameAnchorWidget.Offset(height, EncyclopediaFrameAnchorWidget.NavigationTop, 48f, 22f);

        Assert.IsTrue(frame + HeaderTop >= 0f, "header top on screen");
        Assert.IsTrue(page >= frame + HeaderBottom, $"page top {page} below the header bottom {frame + HeaderBottom}");
        Assert.IsTrue(page >= frame + SearchBoxBottom, "page top below the search box");
        Assert.IsTrue(navigation >= page + 780f, $"navigation {navigation} below the page bottom {page + 780f}");
        Assert.IsTrue(navigation + 48f + 22f <= height + 0.001f, "navigation keeps its bottom margin");
    }

    [TestMethod]
    public void Offsets_ShortViewport_KeepTheHeaderAndNavigationOnScreen()
    {
        // 900 cannot hold 72 + 780 + 70: the header stays whole at the top, the page rises to 8 under it, and the
        // navigation stays whole at the bottom over the page's last 30 (the documented small-viewport limit).
        float frame = EncyclopediaFrameAnchorWidget.FrameTop(900f);
        float page = EncyclopediaFrameAnchorWidget.Offset(900f, EncyclopediaFrameAnchorWidget.PageTop, 780f, EncyclopediaFrameAnchorWidget.PageClearance);
        float navigation = EncyclopediaFrameAnchorWidget.Offset(900f, EncyclopediaFrameAnchorWidget.NavigationTop, 48f, 22f);

        Assert.AreEqual(0f, frame, 0.001f, "header at y 12..72, whole");
        Assert.AreEqual(80f, page, 0.001f);
        Assert.AreEqual(830f, navigation, 0.001f);
    }

    [TestMethod]
    public void Offsets_938_IsTheShortestViewportWithNoOverlap()
    {
        float page = EncyclopediaFrameAnchorWidget.Offset(938f, EncyclopediaFrameAnchorWidget.PageTop, 780f, EncyclopediaFrameAnchorWidget.PageClearance);
        float navigation = EncyclopediaFrameAnchorWidget.Offset(938f, EncyclopediaFrameAnchorWidget.NavigationTop, 48f, 22f);

        Assert.IsTrue(page + 780f <= navigation, $"page bottom {page + 780f}, navigation {navigation}");
        Assert.IsTrue(page >= HeaderBottom);
    }

    [TestMethod]
    public void Offset_NaNClearance_IsIgnored()
    {
        Assert.AreEqual(1010f, EncyclopediaFrameAnchorWidget.Offset(1080f, EncyclopediaFrameAnchorWidget.NavigationTop, 48f, float.NaN), 0.001f);
    }

    [DataTestMethod]
    [DataRow(1080f, 1f, 1080f, DisplayName = "1920x1080 at UI scale 1")]
    [DataRow(1440f, 1080f / 1440f, 1080f, DisplayName = "2560x1440 and 5120x1440: scale 1440/1080")]
    [DataRow(1080f, 1f / 1.2f, 900f, DisplayName = "1920x1080 at UI scale 1.2")]
    [DataRow(1200f, 1f / 1.0204f, 1176f, DisplayName = "1920x1200: width-bound scale")]
    public void TryLogicalHeight_PhysicalPixels_DividesOutTheScale(float physical, float inverseScale, float expected)
    {
        Assert.IsTrue(EncyclopediaFrameAnchorWidget.TryLogicalHeight(physical, inverseScale, out float logical));
        Assert.AreEqual(expected, logical, 0.5f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(0f, 1f)]
    [DataRow(1080f, float.NaN)]
    [DataRow(1080f, 0f)]
    [DataRow(float.PositiveInfinity, 1f)]
    public void TryLogicalHeight_NotYetLaidOut_Refuses(float physical, float inverseScale)
    {
        Assert.IsFalse(EncyclopediaFrameAnchorWidget.TryLogicalHeight(physical, inverseScale, out _));
    }
}
