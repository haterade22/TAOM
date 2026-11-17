using System.Globalization;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #1212. The native encyclopedia loads the bar movie first and the page movie after it on one layer, so
/// the page drew and hit-tested above the header and the search results. The bar root is now an
/// EncyclopediaOverlayWidget that keeps its movie last and moves the dimmer under every page; the bar and each
/// page place their canvases from one frame reference (EncyclopediaFrameAnchorWidget).
/// </summary>
[TestClass]
public class EncyclopediaOverlayPrefabTests
{
    private const string Dir = "Main/_Module/GUI/PreFabs/FactionUI/";
    private const string Anchor = "EncyclopediaFrameAnchorWidget";

    private static XmlDocument Load(string path)
    {
        var doc = new XmlDocument();
        doc.Load(RepoPaths.RepoPath(path));
        return doc;
    }

    private static XmlElement Bar() => (XmlElement)Load(Dir + "TAOMEncyclopediaBar.xml").SelectSingleNode("/Prefab/Window/*")!;

    private static float Float(XmlElement element, string attribute) =>
        float.Parse(element.GetAttribute(attribute), CultureInfo.InvariantCulture);

    [TestMethod]
    public void BarRoot_IsTheOverlay_AcceptsEvents_AndHasNoDimmerItself()
    {
        var root = Bar();

        Assert.AreEqual("EncyclopediaOverlayWidget", root.Name);
        // GauntletLayer.HitTest asks only whether a widget that accepts events covers the cursor. The dimmer sits
        // outside every movie, so a root without events let clicks fall through to the screen under the
        // encyclopedia (a Clan screen Done under it crashed EncyclopediaData.OnTick). The root accepts events and
        // the widget passes each click on (BarRoot_PassesEveryMouseEventOn).
        Assert.AreEqual("", root.GetAttribute("DoNotAcceptEvents"), "a root without events lets clicks reach the screen under the encyclopedia");
        Assert.AreEqual("", root.GetAttribute("Sprite"), "a dimmer on the bar root would darken the page under it");
    }

    [TestMethod]
    public void BarRoot_PassesEveryMouseEventOn()
    {
        // Gauntlet hands an event to the first widget under the cursor whose preview accepts it, children before
        // parents. The root sits above every page, so a preview left true would take the page's clicks.
        string[] previews =
        {
            "OnPreviewMousePressed", "OnPreviewMouseReleased", "OnPreviewMouseAlternatePressed",
            "OnPreviewMouseAlternateReleased", "OnPreviewMouseMove",
        };
        var type = typeof(TAOM.Features.FactionUI.Campaign.EncyclopediaOverlayWidget);
        foreach (string name in previews)
        {
            var method = type.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            Assert.AreEqual(type, method!.DeclaringType, name + " must be overridden to return false");
        }
    }

    [TestMethod]
    public void BarBackdrop_IsTheFirstChild_TheOldDimmer_AndAcceptsEvents()
    {
        // The overlay moves this child under every page; it still takes the clicks outside the pages, which
        // close the search results (EncyclopediaSearchBarBrushWidget reads LatestMouseUpWidget).
        var root = Bar();
        string id = root.GetAttribute("BackdropWidget");
        var first = (XmlElement)root.SelectSingleNode("Children/*[1]")!;

        Assert.AreEqual(id, first.GetAttribute("Id"));
        Assert.AreEqual("BlankWhiteSquare", first.GetAttribute("Sprite"));
        Assert.AreEqual("#00000099", first.GetAttribute("Color"));
        Assert.AreNotEqual("true", first.GetAttribute("DoNotAcceptEvents"));
    }

    [TestMethod]
    public void BarFrame_IsAnchoredAtTheFrameTop()
    {
        var frame = (XmlElement)Bar().SelectSingleNode($"//*[@Id='EncyclopediaFrame']")!;

        AssertAnchor(frame, 0f, 0f);
    }

    [DataTestMethod]
    [DataRow("TAOMEncyclopediaHome.xml", "HomeCanvas")]
    [DataRow("TAOMEncyclopediaItemList.xml", "ListCanvas")]
    public void ListCanvases_AreAnchoredAtTheFrameTop(string file, string id)
    {
        var canvas = (XmlElement)Load(Dir + file).SelectSingleNode($"//*[@Id='{id}']")!;

        Assert.IsNotNull(canvas, id);
        AssertAnchor(canvas, 0f, 0f);
    }

    [DataTestMethod]
    [DataRow(Dir + "TAOMEncyclopediaHeroPage.xml", "HeroLedger")]
    [DataRow(Dir + "TAOMEncyclopediaUnitPage.xml", "TroopPage")]
    [DataRow(Dir + "TAOMEncyclopediaClanPage.xml", "ClanFolio")]
    [DataRow(Dir + "TAOMEncyclopediaFactionPage.xml", "KingdomFolio")]
    [DataRow(Dir + "TAOMEncyclopediaSettlementPage.xml", "SettlementFolio")]
    [DataRow("Main/_Module/GUI/PreFabs/Realms/RealmsEncyclopediaTitlePage.xml", "TitleFolio")]
    public void DetailPageCanvas_IsAnchoredBelowTheHeader(string path, string id)
    {
        var canvas = (XmlElement)Load(path).SelectSingleNode($"//*[@Id='{id}']")!;

        Assert.IsNotNull(canvas, id);
        AssertAnchor(canvas, EncyclopediaFrameAnchorWidget.PageTop, EncyclopediaFrameAnchorWidget.PageClearance);
    }

    [TestMethod]
    public void PageNavigation_IsAnchoredBelowThePage_AndKeepsItsBottomMargin()
    {
        var nav = (XmlElement)Load(Dir + "TAOMEncyclopediaPageNavigation.xml").SelectSingleNode("/Prefab/Window/*")!;

        Assert.AreEqual("PageNavigation", nav.GetAttribute("Id"));
        AssertAnchor(nav, EncyclopediaFrameAnchorWidget.NavigationTop, 22f);
    }

    // The XML offset is the 1080 layout, so a frame before the first late update looks like the shipped page.
    private static void AssertAnchor(XmlElement element, float frameOffset, float clearance)
    {
        Assert.AreEqual(Anchor, element.Name, element.GetAttribute("Id"));
        Assert.AreEqual(frameOffset, Float(element, "FrameOffset"), 0.001f);
        if (clearance > 0f)
            Assert.AreEqual(clearance, Float(element, "BottomClearance"), 0.001f);
        else
            Assert.AreEqual("", element.GetAttribute("BottomClearance"));
        Assert.AreEqual("Top", element.GetAttribute("VerticalAlignment"));
        Assert.AreEqual("", element.GetAttribute("MarginTop") + element.GetAttribute("MarginBottom"));
        Assert.AreEqual(EncyclopediaFrameAnchorWidget.Offset(1080f, frameOffset, Float(element, "SuggestedHeight"), clearance),
            Float(element, "PositionYOffset"), 0.001f);
    }
}
