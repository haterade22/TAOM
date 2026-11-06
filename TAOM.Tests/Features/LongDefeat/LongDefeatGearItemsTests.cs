using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>D34 (phase4-plan.md): kits are checked only at the spawn, so the launcher asks which gear sets name an item the object manager lacks; those sets are hidden for the run, one warning each.</summary>
[TestClass]
public class LongDefeatGearItemsTests
{
    private static GearSet Set(string id, params string[] items) =>
        new(id, id, "", GearKind.Armour, null, items.Select((item, i) => new LdKitSlot(new[] { "Head", "Body", "Gloves", "Leg", "Cape" }[i], item)).ToList(), 100);

    private static RunPlan Plan(params GearSet[] sets) =>
        new("helms", new[] { new GarrisonRow("d", FormationSlot.Infantry, 1) }, new WavePlan(new[] { new WaveSpec(1, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(1, 1)),
            new ShopPlan(0, new ShopOffer[0], null, sets.Length == 0 ? null : new LocationsPlan(new ArmouryPlan(4f, sets), null, null)), RunRules.Default, RunOptions.Default);

    /// <summary>An item test that knows these items and fits each of them to every slot.</summary>
    private static Func<string, string, bool> Known(params string[] items) => (slot, item) => items.Contains(item);

    [TestMethod]
    public void EveryItemThere_HidesNothing_AndWarnsOfNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var known = Known("h1", "b1", "h2");

        var hidden = LongDefeatGearItems.Missing(Plan(Set("a", "h1", "b1"), Set("b", "h2")), known, logger);

        Assert.AreEqual(0, hidden.Count);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ASetWithAMissingItem_IsHidden_WithOneWarningNamingTheSetAndTheItem()
    {
        var logger = Substitute.For<IModLogger>();

        var hidden = LongDefeatGearItems.Missing(Plan(Set("a", "h1", "gone"), Set("b", "h2")), Known("h1", "h2"), logger);

        CollectionAssert.AreEqual(new[] { "a" }, hidden.ToArray());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("'a'") && m.Contains("gone") && !m.Contains("h1")));
    }

    [TestMethod]
    public void ASetMissingTwoItems_IsOneWarning_NamingBoth_AndHiddenOnce()
    {
        var logger = Substitute.For<IModLogger>();

        var hidden = LongDefeatGearItems.Missing(Plan(Set("a", "gone1", "gone2", "h1")), Known("h1"), logger);

        CollectionAssert.AreEqual(new[] { "a" }, hidden.ToArray());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gone1") && m.Contains("gone2")));
    }

    [TestMethod]
    public void EachBadSet_GetsItsOwnWarning_AndTheIdsComeBackInShopOrder()
    {
        var logger = Substitute.For<IModLogger>();

        var hidden = LongDefeatGearItems.Missing(Plan(Set("z", "x1"), Set("good", "h1"), Set("a", "x2")), Known("h1"), logger);

        CollectionAssert.AreEqual(new[] { "z", "a" }, hidden.ToArray());
        logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ARunWithNoArmoury_HasNothingToCheck_AndAsksTheObjectManagerNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var asked = new List<string>();

        var hidden = LongDefeatGearItems.Missing(Plan(), (slot, id) =>
        {
            asked.Add(id);
            return false;
        }, logger);

        Assert.AreEqual(0, hidden.Count);
        Assert.AreEqual(0, asked.Count);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void AnItemUsedByTwoSets_IsLookedUpOncePerSet_AndHidesBoth()
    {
        var logger = Substitute.For<IModLogger>();

        var hidden = LongDefeatGearItems.Missing(Plan(Set("a", "shared"), Set("b", "shared")), Known(), logger);

        CollectionAssert.AreEqual(new[] { "a", "b" }, hidden.ToArray());
    }

    [TestMethod]
    public void TheLauncher_HandsTheHiddenIdsToThePlan_InOneLine()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs", stripComments: true);

        StringAssert.Contains(source, "HiddenGearSets = LongDefeatGearItems.Missing(plan, objects, _logger),");
    }

    [TestMethod]
    public void TheEngineOverload_AsksTheObjectManagerForAnItemObject()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatGearItems.cs", stripComments: true);

        StringAssert.Contains(source, "GetObject<ItemObject>(id)");
    }

    [TestMethod]
    public void AnItemThatDoesNotFitItsSlot_HidesTheSet_AsAMissingOneDoes_AndTheWarningNamesTheSlot()
    {
        var logger = Substitute.For<IModLogger>();
        Func<string, string, bool> fitsOnlyItsOwnSlot = (slot, item) => item == "cape1" ? slot == "Cape" : true;

        var hidden = LongDefeatGearItems.Missing(Plan(Set("a", "h1", "cape1"), Set("b", "h2")), fitsOnlyItsOwnSlot, logger);

        CollectionAssert.AreEqual(new[] { "a" }, hidden.ToArray(), "cape1 sits under Body in set a");
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("'a'") && m.Contains("Body") && m.Contains("cape1") && !m.Contains("h1")));
    }

    [TestMethod]
    public void TheEngineOverload_AppliesTheRuleBuildKitAppliesToASlot_ThroughTheSameNameMapping()
    {
        // BuildKit (LongDefeatBattlefieldAdapter.Player.cs) refuses an item that is unknown or misfit; the launch check must hide exactly what the spawn would refuse.
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatGearItems.cs", stripComments: true);

        StringAssert.Contains(source, "Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slot)");
        StringAssert.Contains(source, "Equipment.IsItemFitsToSlot(index, item)");
    }
}
