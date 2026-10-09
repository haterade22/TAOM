using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FiefManagement;

/// <summary>
/// Source and data pins for the "Fiefs" navigation button (#789): F6 and the button share ONE opener and
/// ONE map-menu gate, the two brushes carry a layer for the element's StringId, and the sprite that layer
/// names exists.
/// </summary>
[TestClass]
public class FiefsNavButtonWiringTests
{
    private const string MapBarBrushes = "Main/_Module/GUI/Brushes/MapBar.xml";
    private const string SpriteData = "Main/_Module/GUI/TAOMSpriteData.xml";
    private const string GateFile = "Main/Adapters/MapMenuGate.cs";

    // ---------- brushes ----------

    [DataTestMethod]
    [DataRow("MapBar.Left.Button.Backgrounds")]
    [DataRow("MapBar.Left.Icons")]
    public void MapBarBrush_HasExactlyOneLayerForTheFiefsElement(string brushName)
    {
        var layers = LayersOf(brushName)
            .Where(l => (string?)l.Attribute("Name") == TaomFiefsNavigationElement.ElementId)
            .ToList();

        Assert.AreEqual(1, layers.Count,
            $"{brushName} needs one BrushLayer named '{TaomFiefsNavigationElement.ElementId}': MapBar.xml renders IconID=\"@ItemId\", so a missing layer draws nothing.");
        Assert.IsFalse(string.IsNullOrWhiteSpace((string?)layers[0].Attribute("Sprite")), brushName + " layer has no Sprite.");
    }

    [TestMethod]
    public void IconLayer_NamesASpriteTheTaomSpriteSheetDeclares()
    {
        var sprite = (string?)LayersOf("MapBar.Left.Icons")
            .Single(l => (string?)l.Attribute("Name") == TaomFiefsNavigationElement.ElementId)
            .Attribute("Sprite");

        var doc = XDocument.Parse(ReadSource(SpriteData));
        var parts = doc.Descendants("SpritePart").Select(p => (string?)p.Element("Name")).ToList();
        var generics = doc.Descendants("GenericSprite").Select(p => (string?)p.Element("Name")).ToList();

        CollectionAssert.Contains(parts, sprite, $"TAOMSpriteData.xml declares no SpritePart '{sprite}'.");
        CollectionAssert.Contains(generics, sprite, $"TAOMSpriteData.xml declares no GenericSprite '{sprite}'.");
    }

    [TestMethod]
    public void TheFiefsElementId_DoesNotCollideWithAnotherLayerInTheIconBrush()
    {
        var names = LayersOf("MapBar.Left.Icons").Select(l => (string?)l.Attribute("Name")).ToList();

        Assert.AreEqual(names.Count, names.Distinct().Count(), "Duplicate layer names in MapBar.Left.Icons.");
    }

    // ---------- F6 and the button share one opener and one gate ----------

    [TestMethod]
    public void F6Patch_RoutesThroughTheSharedOpener_AndKeepsNoGuardOfItsOwn()
    {
        var source = ReadSource("Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs", stripComments: true);

        StringAssert.Contains(source, "FiefHubOpener");
        StringAssert.Contains(source, ".TryOpen(");
        StringAssert.Contains(source, "IsF6Pressed");
        Assert.IsFalse(source.Contains("ActivateGameMenu"), "F6 must not open the menu itself; the opener owns that.");
        Assert.IsFalse(source.Contains("IsInArmyManagement"), "The modal guards live in MapMenuGate now.");
        Assert.IsFalse(source.Contains("InformationManager"), "The no-fief message lives in the host adapter now.");
    }

    [TestMethod]
    public void NavElement_OpensThroughTheSharedOpener_AndNeverActivatesTheMenuItself()
    {
        var source = ReadSource("Main/Features/FiefManagement/UI/TaomFiefsNavigationElement.cs", stripComments: true);

        StringAssert.Contains(source, "FiefHubOpener");
        StringAssert.Contains(source, ".TryOpen(");
        StringAssert.Contains(source, ".GetAvailability()");
        StringAssert.Contains(source, "IsNavigationBarEnabled");
        Assert.IsFalse(source.Contains("ActivateGameMenu"));
    }

    [TestMethod]
    public void NavElement_TakesItsDependenciesThroughTheConstructor()
    {
        // The postfix factory resolves them once; a service locator call inside the element would resolve
        // on every construction and hide a missing registration behind a null check.
        var source = ReadSource("Main/Features/FiefManagement/UI/TaomFiefsNavigationElement.cs", stripComments: true);

        Assert.IsFalse(source.Contains("IoC.Resolve"), "The element must not resolve IoC itself.");
    }

    [TestMethod]
    public void NavElement_NeverReportsItselfActive()
    {
        // fief_hub is a game menu, not a pushed state: an active element would leak the bar's selected look onto pushed screens.
        var source = ReadSource("Main/Features/FiefManagement/UI/TaomFiefsNavigationElement.cs", stripComments: true);

        StringAssert.Matches(source, new Regex(@"IsActive\s*=>\s*false"));
        StringAssert.Matches(source, new Regex(@"IsLockingNavigation\s*=>\s*false"));
        StringAssert.Matches(source, new Regex(@"HasAlert\s*=>\s*false"));
    }

    [TestMethod]
    public void TheMapModalGuardList_IsWrittenOnce()
    {
        // Three copies of this list drifted before (F6, the field camp's query, and the nav button). One
        // gate now; every other file delegates. IsMapIncidentActive is the member only the list names.
        var mainDir = RepoPath("Main");
        var holders = Directory.EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => StripComments(File.ReadAllText(f)).Contains("IsMapIncidentActive"))
            .Select(f => Path.GetFullPath(f).Substring(Path.GetFullPath(RepoPath()).Length).TrimStart('\\', '/').Replace('\\', '/'))
            .OrderBy(f => f)
            .ToList();

        CollectionAssert.AreEqual(new[] { GateFile }, holders,
            "Only MapMenuGate.cs may name the map's modal flags. Holders: " + string.Join(", ", holders));
    }

    [DataTestMethod]
    [DataRow("Main/Adapters/FiefHubHostAdapter.cs")]
    [DataRow("Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs")]
    public void TheGateConsumers_DelegateToMapMenuGate(string file)
    {
        StringAssert.Contains(ReadSource(file, stripComments: true), "MapMenuGate.IsMapClearForMenu()");
    }

    [TestMethod]
    public void TheGate_MirrorsVanillasFullSet_PlusTheStaleFlagAndEscapeMenuCases()
    {
        var source = ReadSource(GateFile, stripComments: true);

        foreach (var term in new[]
                 {
                     "ActiveState is MapState", "CurrentMenuContext", "IsInMenu", "IsInBattleSimulation", "IsInArmyManagement",
                     "IsMarriageOfferPopupActive", "IsHeirSelectionPopupActive", "IsMapCheatsActive", "IsMapIncidentActive",
                     "IsOverlayContextMenuEnabled", "IsEncyclopediaOpen", "IsEscapeMenuOpened",
                     "GetMapScreenActionIsEnabledWithReason",
                 })
            StringAssert.Contains(source, term, "MapMenuGate lost a guard: " + term);

        // The helper reads Hero.MainHero and MobileParty.MainParty unguarded, so it must come after the map-state check.
        Assert.IsTrue(source.IndexOf("ActiveState is MapState") < source.IndexOf("GetMapScreenActionIsEnabledWithReason"),
            "The helper must run after the MapState check.");
    }

    [TestMethod]
    public void FiefManagementIoC_ClosesTheOpenerGraphInARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        FiefManagementIoC.RegisterFiefManagementFeature(container);

        var errors = container.Validate(typeof(FiefHubOpener));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }

    // ---------- the brush reads ----------

    private static System.Collections.Generic.IEnumerable<XElement> LayersOf(string brushName)
    {
        var doc = XDocument.Parse(ReadSource(MapBarBrushes));
        var brush = doc.Descendants("Brush").Single(b => (string?)b.Attribute("Name") == brushName);
        return brush.Element("Layers")!.Elements("BrushLayer");
    }
}
