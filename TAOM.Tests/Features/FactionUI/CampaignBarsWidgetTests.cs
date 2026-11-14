using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TAOM.Features.FactionUI.Campaign;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The three campaign bars (navigation, time, stats, with the muster plate) hide while a native screen or a
/// full-screen map overlay is open, and show again on the bare map. Each state below is the one
/// GauntletMapBarGlobalLayer.OnTick writes into MapTimeControlVM every frame (v1.5.7): IsInMap is
/// <c>TopScreen is MapScreen</c>, the overlay flags are copied from MapScreen. The widget recomputes every frame
/// from those flags, so a sequence of states is a sequence of calls with no carried latch.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CampaignBarsWidgetTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMMapBar.xml";

    // MapTimeControlVM's flags are plain auto-properties; skipping the constructor keeps the test off the campaign.
    private static MapTimeControlVM OnMap()
    {
        var time = (MapTimeControlVM)FormatterServices.GetUninitializedObject(typeof(MapTimeControlVM));
        time.IsInMap = true;
        return time;
    }

    [TestMethod]
    public void ShouldShow_BareMap_Shows()
    {
        Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, OnMap()));
    }

    [TestMethod]
    public void ShouldShow_NativeScreenOnTop_Hides()
    {
        // Character, Inventory, Party, Quests, Clan, Kingdom and every other ScreenManager screen over the map.
        var time = OnMap();
        time.IsInMap = false;
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time));
    }

    [TestMethod]
    public void ShouldShow_EachFullScreenMapOverlay_Hides()
    {
        void Check(string name, System.Action<MapTimeControlVM> open)
        {
            var time = OnMap();
            open(time);
            Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time), name);
        }

        Check("encyclopedia", t => t.IsEncyclopediaOpen = true);
        Check("army management", t => t.IsInArmyManagement = true);
        Check("town management", t => t.IsInTownManagement = true);
        Check("recruitment", t => t.IsInRecruitment = true);
        Check("hideout troop manage", t => t.IsInHideoutTroopManage = true);
        Check("campaign options", t => t.IsInCampaignOptions = true);
        Check("battle simulation", t => t.IsInBattleSimulation = true);
    }

    [TestMethod]
    public void ShouldShow_MapPopupsAndEscapeMenu_StayVisible()
    {
        // Small popups over the map keep the bars: they are not full-screen and vanilla only locks the time panel.
        void Check(string name, System.Action<MapTimeControlVM> open)
        {
            var time = OnMap();
            open(time);
            Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, time), name);
        }

        Check("escape menu", t => t.IsEscapeMenuOpened = true);
        Check("marriage offer", t => t.IsMarriageOfferPopupActive = true);
        Check("heir selection", t => t.IsHeirSelectionPopupActive = true);
        Check("map incident", t => t.IsMapIncidentActive = true);
        Check("map cheats", t => t.IsMapCheatsActive = true);
        Check("overlay context menu", t => t.IsOverlayContextMenuEnabled = true);
    }

    [TestMethod]
    public void ShouldShow_NestedScreenClosedOverAnother_StaysHiddenUntilMap()
    {
        // Party opened from Character: closing Party leaves Character on top, so IsInMap stays false.
        var time = OnMap();
        time.IsInMap = false;
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time), "outer screen");
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time), "inner screen closed, outer still open");
        time.IsInMap = true;
        Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, time), "final close back to the map");
    }

    [TestMethod]
    public void ShouldShow_EncyclopediaClosedOnMap_Restores()
    {
        var time = OnMap();
        time.IsEncyclopediaOpen = true;
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time));
        time.IsEncyclopediaOpen = false;
        Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, time));
    }

    [TestMethod]
    public void ShouldShow_NativeDisabled_StaysHiddenOnMap()
    {
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(false, OnMap()));
    }

    [TestMethod]
    public void ShouldShow_NoModelAttached_FollowsNativeEnabled()
    {
        // Before OnLoaded attaches the view model, and after a movie rebuild, the root's native IsVisible governs.
        Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, null));
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(false, null));
    }

    [TestMethod]
    public void ShouldShow_ReopenAfterClose_HidesAgain()
    {
        var time = OnMap();
        time.IsInMap = false;
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time));
        time.IsInMap = true;
        Assert.IsTrue(CampaignBarsWidget.ShouldShow(true, time));
        time.IsInMap = false;
        Assert.IsFalse(CampaignBarsWidget.ShouldShow(true, time), "second open");
    }

    [TestMethod]
    public void MapBarPrefab_WrapsAllBarsInOneCampaignBarsWidget()
    {
        var root = XDocument.Parse(ReadSource(PrefabPath)).Root!;
        var wrappers = root.Descendants("CampaignBarsWidget").ToList();
        Assert.AreEqual(1, wrappers.Count, "one wrapper");
        var wrapper = wrappers[0];
        Assert.AreEqual("true", (string?)wrapper.Attribute("DoNotAcceptEvents"), "the wrapper takes no clicks itself");
        Assert.IsNull(wrapper.Attribute("IsVisible"), "visibility is the widget's own per-frame write, not a binding");
        Assert.IsNull(wrapper.Attribute("IsEnabled"), "native enabled state stays on the bars");

        var bars = wrapper.Element("Children")!.Elements().Select(e => e.Name.LocalName).ToList();
        CollectionAssert.AreEqual(
            new[] { "TAOMCampaignNavigation", "TAOMCampaignTime", "TAOMCampaignStats", "TAOMCampaignMuster" },
            bars,
            "the bars are direct siblings, so the muster plate's ..\\InfoBarWidget path still resolves");
        foreach (var name in new[] { "TAOMCampaignNavigation", "TAOMCampaignTime", "TAOMCampaignStats", "TAOMCampaignMuster" })
            Assert.AreEqual(1, root.Descendants(name).Count(), name + " appears once, inside the wrapper");
    }

    [TestMethod]
    public void OnLoaded_ShippedMapBarPrefab_AttachesModelToCampaignBarsWidget()
    {
        // The installed Widget.FindChildrenWithType<T> skips every widget whose Id is null or empty (v1.5.7 IL_0024..002e),
        // so the wrapper is found only if the prefab names it. The tree mirrors the shipped prefab, Id for Id, and the
        // engine's own lookup runs over it through the production OnLoaded.
        var window = XDocument.Parse(ReadSource(PrefabPath)).Root!.Element("Window")!.Elements().Single();
        var root = BuildWidget(window);
        var map = (MapBarVM)FormatterServices.GetUninitializedObject(typeof(MapBarVM));
        var movie = Substitute.For<IGauntletMovie>();
        movie.RootWidget.Returns(root);
        // The identifier's constructor is internal; LoadMovie sets the same three properties.
        var loaded = (GauntletMovieIdentifier)FormatterServices.GetUninitializedObject(typeof(GauntletMovieIdentifier));
        typeof(GauntletMovieIdentifier).GetProperty(nameof(GauntletMovieIdentifier.MovieName))!.SetValue(loaded, "TAOMMapBar");
        typeof(GauntletMovieIdentifier).GetProperty(nameof(GauntletMovieIdentifier.DataSource))!.SetValue(loaded, map);
        typeof(GauntletMovieIdentifier).GetProperty(nameof(GauntletMovieIdentifier.Movie))!.SetValue(loaded, movie);

        CampaignPresentation.OnLoaded(loaded, map);

        var bars = root.GetAllChildrenRecursive().OfType<CampaignBarsWidget>().Single();
        Assert.AreSame(map, bars.Model, "OnLoaded never attached the map bar model, so the bars stay visible over every screen");
    }

    // A bare widget per prefab element: FindChildrenWithType and GetAllChildrenRecursive read only _children and _id.
    private static Widget BuildWidget(XElement element)
    {
        var type = element.Name.LocalName == nameof(CampaignBarsWidget) ? typeof(CampaignBarsWidget) : typeof(Widget);
        var widget = (Widget)FormatterServices.GetUninitializedObject(type);
        var children = new List<Widget>();
        WidgetField("_children").SetValue(widget, children);
        WidgetField("_id").SetValue(widget, (string?)element.Attribute("Id"));
        foreach (var child in element.Element("Children")?.Elements() ?? Enumerable.Empty<XElement>())
            children.Add(BuildWidget(child));
        return widget;
    }

    private static FieldInfo WidgetField(string name)
    {
        var field = typeof(Widget).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "the installed Widget has no " + name + " field");
        return field!;
    }
}
