using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #1532. TAOMArmyOverlay.xml is the themed army panel, swapped in for SandBox's ArmyOverlay movie. The panel
/// sits in ArmyOverlayClipWidget, cut at the stats plate's frame, so its visual states only set how much stands above
/// that edge. These tests pin the numbers that must agree with each other inside the prefab.
/// </summary>
[TestClass]
public class ArmyOverlayPrefabTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMArmyOverlay.xml";

    private static XmlDocument Load()
    {
        var doc = new XmlDocument();
        doc.Load(RepoPaths.RepoPath(PrefabPath));
        return doc;
    }

    private static XmlElement ById(XmlDocument doc, string id) =>
        (XmlElement)doc.SelectSingleNode($"//*[@Id='{id}']")!;

    private static float Float(XmlElement element, string attribute, float fallback = 0f) =>
        element.HasAttribute(attribute) ? float.Parse(element.GetAttribute(attribute), CultureInfo.InvariantCulture) : fallback;

    private static float StateOffset(XmlDocument doc, string state) =>
        Float((XmlElement)doc.SelectSingleNode($"//VisualDefinition[@Name='Overlay']/VisualState[@State='{state}']")!, "PositionYOffset");

    private static XmlElement Root(XmlDocument doc) => (XmlElement)doc.SelectSingleNode("/Prefab/Window/ArmyOverlayWidget")!;

    [TestMethod]
    public void Panel_SitsInTheClip_AndTheRootFindsItThere()
    {
        var doc = Load();
        var overlay = ById(doc, "OverlayWidget");
        var clip = (XmlElement)overlay.ParentNode!.ParentNode!;
        var root = Root(doc);

        Assert.AreEqual("ArmyOverlayClipWidget", clip.Name);
        Assert.AreEqual("true", clip.GetAttribute("ClipContents"));
        // The cut follows the stats panel's own expansion, not a flag with a clock of its own, and the widget sets
        // its first cut itself, so the prefab holds no copy of it.
        Assert.IsFalse(clip.HasAttribute("IsInfoBarExtended"), "the clip must not run its own slide");
        Assert.IsFalse(clip.HasAttribute("MarginBottom"), "the clip's cut comes from ArmyOverlayClipWidget");
        foreach (var reference in new[] { "Overlay", "ArmyListGridWidget", "ExtendButton" })
            StringAssert.StartsWith(root.GetAttribute(reference), $"{clip.GetAttribute("Id")}\\OverlayWidget", reference);
    }

    [TestMethod]
    public void EveryWidgetReference_ResolvesToAWidget()
    {
        // No gate pairs a swapped prefab with vanilla's, so this walks each reference path by Id from the widget
        // that names it, as Gauntlet does; a broken path leaves vanilla's widget with a null reference. PopupWidget
        // is left out: its target lives in the OverlayPopup prefab, kept from vanilla.
        var doc = Load();
        string[] references =
        {
            "Overlay", "ArmyListGridWidget", "ExtendButton", "Container", "NextPageButton", "PageText",
            "FillWidget", "TutorialFrameWidget", "BackgroundImageWidget", "CurrentCharacterImageWidget",
        };
        var checkedPaths = 0;
        foreach (XmlElement owner in doc.SelectNodes("//*")!)
        {
            foreach (var reference in references)
            {
                if (!owner.HasAttribute(reference))
                    continue;
                var current = owner;
                foreach (var id in owner.GetAttribute(reference).Split('\\'))
                {
                    current = current?.SelectSingleNode($"Children/*[@Id='{id}'] | ItemTemplate/*[@Id='{id}']") as XmlElement;
                    Assert.IsNotNull(current, $"{owner.Name}.{reference}=\"{owner.GetAttribute(reference)}\" does not resolve at '{id}'");
                }
                checkedPaths++;
            }
        }
        Assert.AreEqual(references.Length, checkedPaths, "a reference this test names is missing from the prefab");
    }

    [TestMethod]
    public void Root_HasNoPageControl_SoVanillaNeverShiftsAOnePagePanel()
    {
        // ArmyOverlayWidget subscribes its page-count handler only in the PageControlWidget setter, and on one page
        // that handler moves the panel 40 px right (off the screen edge) and the chevron 40 px left.
        Assert.IsFalse(Root(Load()).HasAttribute("PageControlWidget"));
    }

    [TestMethod]
    public void ClosedPanel_ShowsTheWholeHeader_WhicheverWayTheStatsBarIs()
    {
        var doc = Load();
        var overlay = ById(doc, "OverlayWidget");
        var header = ById(doc, "ArmyOverlayTopControls");
        float closed = StateOffset(doc, "MapNormalOverlayNormal");

        // The clip follows the stats bar, so the panel's own offset must not change with it.
        Assert.AreEqual(closed, StateOffset(doc, "MapExtendedOverlayNormal"));
        Assert.AreEqual(closed, Float(overlay, "PositionYOffset"), "the first frame starts closed");
        // Heights above the screen edge with the stats bar closed; along its length the plate covers up to its
        // frame top, higher than the cut, which reaches down to the right shoulder.
        float panelTop = ArmyOverlayClipWidget.CollapsedClipBottom + Float(overlay, "SuggestedHeight") - closed;
        float frameTop = ArmyOverlayClipTests.FrameTopAt(15);
        float headerBottom = panelTop - Float(header, "MarginTop") - Float(header, "SuggestedHeight");
        float gridTop = panelTop - Float(ById(doc, "GridControlWidget"), "MarginTop");

        Assert.IsTrue(headerBottom >= frameTop, $"the header ends at {headerBottom}, under the plate's frame at {frameTop}");
        Assert.IsTrue(gridTop <= frameTop, $"a closed panel shows the first portrait row above the plate ({gridTop} > {frameTop})");
        // The pager stands over the shoulder, where the panel shows down to the cut.
        foreach (var id in new[] { "NextButton", "PageText" })
        {
            float top = gridTop - Float(ById(doc, id), "MarginTop");
            Assert.IsTrue(top <= ArmyOverlayClipWidget.CollapsedClipBottom, $"a closed panel shows {id} above the shoulder ({top})");
        }
    }

    [TestMethod]
    public void OnePage_FitsInsideThePlate_LeftOfThePager()
    {
        var doc = Load();
        var overlay = ById(doc, "OverlayWidget");
        var control = ById(doc, "GridControlWidget");
        var grid = ById(doc, "PartyList");
        var pageText = ById(doc, "PageText");
        int perPage = int.Parse(control.GetAttribute("ItemPerPage"), CultureInfo.InvariantCulture);
        int columns = int.Parse(grid.GetAttribute("ColumnCount"), CultureInfo.InvariantCulture);

        Assert.AreEqual(0, perPage % columns, "a page ends part way through a row");
        float rows = perPage / columns;
        // The plate's bottom border (its dark outline) starts at cb_army_plate.png row 484 of 527: 19 px up.
        Assert.IsTrue(Float(control, "MarginTop") + rows * Float(grid, "DefaultCellHeight") <= Float(overlay, "SuggestedHeight") - 19f,
            "the last row of a page runs into the plate's bottom border");
        float pagerLeft = Float(overlay, "SuggestedWidth") - Float(pageText, "MarginRight") - Float(pageText, "SuggestedWidth");
        Assert.IsTrue(Float(grid, "MarginLeft") + columns * Float(grid, "DefaultCellWidth") <= pagerLeft,
            "the last column runs under the page number");
        var next = ById(doc, "NextButton");
        Assert.AreEqual(Float(next, "MarginRight") + Float(next, "SuggestedWidth"), Float(pageText, "MarginRight"),
            "the page number sits just left of the page button");
    }

    [TestMethod]
    public void OneRowPage_ShowsThePageButtonAndNumber()
    {
        // ArmyOverlayWidget raises an open panel by the grid's height, so a last page of one row shows only that
        // row's height below the closed position. The pager must fit inside it.
        var doc = Load();
        var overlay = ById(doc, "OverlayWidget");
        var control = ById(doc, "GridControlWidget");
        float shown = Float(overlay, "SuggestedHeight") - (StateOffset(doc, "MapNormalOverlayNormal") - Float(ById(doc, "PartyList"), "DefaultCellHeight"));

        foreach (var id in new[] { "NextButton", "PageText" })
        {
            var element = ById(doc, id);
            Assert.AreEqual("Fixed", element.GetAttribute("HeightSizePolicy"), id);
            float bottom = Float(control, "MarginTop") + Float(element, "MarginTop") + Float(element, "SuggestedHeight");
            Assert.IsTrue(bottom <= shown, $"{id} ends at {bottom}, below the {shown} px a one-row page shows");
        }
    }

    [TestMethod]
    public void CohesionTrack_IsTallEnoughForItsNineSliceBorders()
    {
        // cc_slider_track.nine holds left, right, top and bottom borders; a shorter widget squeezes them.
        var borders = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "GUI", "FactionUI", "RuntimeSprites", "cc_slider_track.nine"))
            .Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        var track = (XmlElement)Load().SelectSingleNode("//*[@Brush='TAOM.Army.Cohesion.Track']/../..")!;

        Assert.IsTrue(Float(track, "SuggestedHeight") >= borders[2] + borders[3], $"the track is {Float(track, "SuggestedHeight")} px tall");
    }

    [TestMethod]
    public void CohesionHint_ListensToTheButtonAndToTheWidgetAroundIt()
    {
        // A HintWidget relays its parent's HoverBegin, and the engine hovers only the top widget that takes the
        // mouse: the enabled cohesion button while the player can manage the army, else the widget around it.
        var parents = Load().SelectNodes("//HintWidget[@DataSource='{CohesionHint}']")!.Cast<XmlNode>()
            .Select(hint => (XmlElement)hint.ParentNode!.ParentNode!).ToList();

        Assert.AreEqual(2, parents.Count);
        var button = parents.Single(p => p.Name == "ButtonWidget");
        Assert.AreEqual("ExecuteOpenArmyManagement", button.GetAttribute("Command.Click"));
        var around = parents.Single(p => p != button);
        Assert.AreSame(button.ParentNode!.ParentNode, around);
        Assert.AreEqual("true", button.GetAttribute("DoNotPassEventsToChildren"));
        Assert.AreEqual("", around.GetAttribute("DoNotAcceptEvents"), "the widget around a disabled button must take the hover");
    }

    [TestMethod]
    public void EveryVanillaBindingAndCommand_IsKept()
    {
        // The VM is vanilla's ArmyMenuOverlayVM; a binding lost in the restyle fails silently in game.
        string xml = Load().OuterXml;
        string[] kept =
        {
            "ExecuteOpenArmyManagement", "@IsPlayerArmyLeader", "@CanManageArmy", "{ManageArmyHint}", "@Cohesion",
            "@IsCohesionWarningEnabled", "{CohesionHint}", "{FoodHint}", "@Food", "{ManCountHint}", "@ManCountText",
            "{PartyList}", "ExecuteSetAsContextMenuItem", "ExecuteOpenTooltip", "ExecuteCloseTooltip", "@IsMergedWithArmy",
            "@PartySizeLbl", "@ShipCount", "{Quests}", "@IsLeader", "{TutorialNotification}", "@ElementID", "@NameText",
            "@DescriptionText", "@LocationText", "@Relation", "{Visual}", "@IsInfoBarExtended",
        };
        var missing = kept.Where(token => !xml.Contains(token)).ToList();
        Assert.AreEqual(0, missing.Count, "lost: " + string.Join(", ", missing));
    }
}
