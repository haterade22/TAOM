using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.ViewModels;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionSelectionVMMusicTests
{
    [TestMethod]
    public void ExecuteSelectRegion_PlayableCultureRegionSignalsSelectedCulture()
    {
        var signaledCultures = new List<string>();
        var selectionService = Substitute.For<IFactionSelectionService>();
        selectionService.SelectRegion("kingdom_of_harad").Returns(new FactionSelectionResult
        {
            Found = true,
            Playable = true,
            HasCulture = true,
            CultureId = "aserai",
        });

        var vm = CreateVm(selectionService, signaledCultures.Add);
        SetLastClickedRegionName("kingdom_of_harad");

        vm.ExecuteSelectRegion();

        CollectionAssert.AreEqual(new[] { "aserai" }, signaledCultures);
    }

    [TestMethod]
    public void ExecuteSelectRegion_NonPlayableRegionDoesNotSignalSelectedCulture()
    {
        var signaledCultures = new List<string>();
        var selectionService = Substitute.For<IFactionSelectionService>();
        selectionService.SelectRegion("deco_region").Returns(new FactionSelectionResult
        {
            Found = true,
            Playable = false,
            HasCulture = false,
            CultureId = "",
        });

        var vm = CreateVm(selectionService, signaledCultures.Add);
        SetLastClickedRegionName("deco_region");

        vm.ExecuteSelectRegion();

        Assert.AreEqual(0, signaledCultures.Count);
    }

    private static FactionSelectionVM CreateVm(
        IFactionSelectionService selectionService,
        Action<string> onCultureSelected)
    {
        var hoverService = Substitute.For<IFactionHoverService>();
        var cultureResolver = Substitute.For<ICultureResolverService>();
        var landmarkService = Substitute.For<ILandmarkService>();
        landmarkService.GetCapitals().Returns(Array.Empty<LandmarkDef>());

        return new FactionSelectionVM(
            _ => { },
            () => { },
            selectionService,
            hoverService,
            cultureResolver,
            landmarkService,
            onCultureSelected);
    }

    private static void SetLastClickedRegionName(string regionName)
    {
        var backingField = typeof(PolygonWidget).GetField(
            "<LastClickedRegionName>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(backingField, "PolygonWidget.LastClickedRegionName backing field must exist.");
        backingField.SetValue(null, regionName);
    }
}
