using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The planner decides, for one game's loaded items, which are governed (character armour and the named
/// weapons), each one's class (the generated table first, the engine tier for armour the table does not
/// list) and which gated pieces must stop being merchandise. Pure, so every rule is pinned here.
/// </summary>
[TestClass]
public class ArmourGatePlannerTests
{
    private static ArmourItemRecord Armour(string id, int tier = 0, bool merch = true, string? culture = "gondor") =>
        new(id, isCharacterArmour: true, engineTier: tier, isMerchandise: merch, cultureId: culture, value: 100);

    private static ArmourItemRecord Weapon(string id, bool merch = true) =>
        new(id, isCharacterArmour: false, engineTier: 0, isMerchandise: merch, cultureId: "gondor", value: 100);

    private static Dictionary<string, ArmourClassEntry> Table(params (string id, ArmourClass cls)[] rows) =>
        rows.ToDictionary(r => r.id, r => new ArmourClassEntry(r.id, r.cls, null), StringComparer.Ordinal);

    private static readonly string[] NoNamed = Array.Empty<string>();

    [TestMethod]
    public void Plan_TabledArmour_TakesTheTableClass()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("a", tier: 0) }, Table(("a", ArmourClass.Elite)), NoNamed, enabled: true);

        Assert.AreEqual(ArmourClass.Elite, plan.ClassById["a"]);
        Assert.AreEqual(1, plan.FromTable);
    }

    [DataTestMethod]
    [DataRow(-1, ArmourClass.Light)]
    [DataRow(0, ArmourClass.Light)]
    [DataRow(1, ArmourClass.Light)]
    [DataRow(2, ArmourClass.Medium)]
    [DataRow(3, ArmourClass.Heavy)]
    [DataRow(4, ArmourClass.Elite)]
    [DataRow(5, ArmourClass.Elite)]
    public void Plan_UntabledArmour_IsClassedByEngineTier(int tier, ArmourClass expected)
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("v", tier) }, Table(), NoNamed, enabled: true);

        Assert.AreEqual(expected, plan.ClassById["v"]);
        Assert.AreEqual(1, plan.FromEngineTier);
    }

    [TestMethod]
    public void Plan_OrdinaryWeapon_IsNotGoverned()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Weapon("sword") }, Table(), NoNamed, enabled: true);

        Assert.IsFalse(plan.ClassById.ContainsKey("sword"));
        Assert.AreEqual(0, plan.ToFlip.Count);
    }

    [TestMethod]
    public void Plan_NamedWeapon_IsNamedAndTakenOutOfTheMarkets()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Weapon("anduril") }, Table(), new[] { "anduril" }, enabled: true);

        Assert.AreEqual(ArmourClass.Named, plan.ClassById["anduril"]);
        CollectionAssert.AreEqual(new[] { "anduril" }, plan.ToFlip.ToArray());
    }

    [TestMethod]
    public void Plan_GatedClasses_AreFlipped_FreeClassesAreNot()
    {
        var items = new[] { Armour("l"), Armour("m"), Armour("h"), Armour("e"), Armour("o"), Armour("c"), Armour("n") };
        var table = Table(("l", ArmourClass.Light), ("m", ArmourClass.Medium), ("h", ArmourClass.Heavy),
            ("e", ArmourClass.Elite), ("o", ArmourClass.Lord), ("c", ArmourClass.Civilian), ("n", ArmourClass.Named));

        var plan = ArmourGatePlanner.Plan(items, table, NoNamed, enabled: true);

        CollectionAssert.AreEquivalent(new[] { "h", "e", "o", "n" }, plan.ToFlip.ToArray());
    }

    [TestMethod]
    public void Plan_PieceAlreadyNotMerchandise_IsClassedButNotFlipped()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("starter", merch: false) }, Table(("starter", ArmourClass.Elite)), NoNamed, enabled: true);

        Assert.AreEqual(ArmourClass.Elite, plan.ClassById["starter"]);
        Assert.AreEqual(0, plan.ToFlip.Count);
    }

    [TestMethod]
    public void Plan_Disabled_ClassesEverythingAndFlipsNothing()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("h") }, Table(("h", ArmourClass.Heavy)), NoNamed, enabled: false);

        Assert.AreEqual(ArmourClass.Heavy, plan.ClassById["h"]);
        Assert.AreEqual(0, plan.ToFlip.Count);
    }

    [TestMethod]
    public void Plan_TableRowForAnUnloadedItem_IsReportedStale()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("a") }, Table(("a", ArmourClass.Light), ("retired", ArmourClass.Heavy)), NoNamed, enabled: true);

        CollectionAssert.AreEqual(new[] { "retired" }, plan.StaleTableIds.ToArray());
        Assert.IsFalse(plan.ClassById.ContainsKey("retired"));
    }

    [TestMethod]
    public void Plan_NamedWeaponNotLoaded_IsReportedMissing()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("a") }, Table(), new[] { "glamdring" }, enabled: true);

        CollectionAssert.AreEqual(new[] { "glamdring" }, plan.MissingNamedWeapons.ToArray());
    }

    [TestMethod]
    public void Plan_NamedListBeatsTheTable()
    {
        // A named id wins even if the generator classed the piece as ordinary armour.
        var plan = ArmourGatePlanner.Plan(new[] { Armour("faramir_cloak") }, Table(("faramir_cloak", ArmourClass.Light)),
            new[] { "faramir_cloak" }, enabled: true);

        Assert.AreEqual(ArmourClass.Named, plan.ClassById["faramir_cloak"]);
    }

    [TestMethod]
    public void Plan_NullOrEmptyIds_AreIgnored()
    {
        var plan = ArmourGatePlanner.Plan(new[] { Armour("") }, Table(), NoNamed, enabled: true);

        Assert.AreEqual(0, plan.ClassById.Count);
    }
}
