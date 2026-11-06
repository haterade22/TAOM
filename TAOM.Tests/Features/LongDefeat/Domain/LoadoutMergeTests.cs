using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>D35 (phase4-plan.md): the lord's effective loadout is the class kit with each owned set laid over it in purchase order, a later set winning a slot.</summary>
[TestClass]
public class LoadoutMergeTests
{
    private static LdKitSlot S(string slot, string item) => new(slot, item);

    private static GearSet Armour(string id, params LdKitSlot[] slots) => new(id, id, "", GearKind.Armour, null, slots, 100);

    private static GearSet Weapons(string id, string[] classes, params LdKitSlot[] slots) => new(id, id, "", GearKind.Weapons, classes, slots, 100);

    private static LdLoadout Sword() =>
        new("sword_and_board", false, new[] { S("Item0", "sword"), S("Item1", "shield"), S("Item2", ""), S("Item3", ""), S("Horse", ""), S("HorseHarness", "") });

    private static LdLoadout Rider() =>
        new("cavalry", true, new[]
        {
            S("Item0", "spear"), S("Item1", "rider_shield"), S("Item2", "rider_sword"), S("Item3", ""),
            S("Head", "rider_helm"), S("Body", "rider_armour"), S("Horse", "charger"), S("HorseHarness", "barding"),
        });

    private static string Show(LdLoadout? loadout) =>
        loadout == null ? "null" : $"{loadout.ClassId}/{loadout.Mounted}: " + string.Join(", ", loadout.Slots.Select(x => x.Slot + "=" + x.ItemId));

    [TestMethod]
    public void NoSetsOwned_IsTheClassKitItself()
    {
        var kit = Sword();

        Assert.AreEqual(Show(kit), Show(LoadoutMerge.Merge(kit, new List<GearSet>())));
    }

    [TestMethod]
    public void NoKit_IsNoLoadout_ALordInHisOwnGearBuysNothing()
    {
        Assert.IsNull(LoadoutMerge.Merge(null, new[] { Armour("a", S("Head", "helm")) }));
    }

    [TestMethod]
    public void AnArmourSet_AddsItsSlotsAfterTheKitSlots_AndKeepsTheClassAndTheMountFlag()
    {
        var merged = LoadoutMerge.Merge(Sword(), new[] { Armour("a", S("Head", "helm"), S("Body", "mail")) })!;

        Assert.AreEqual("sword_and_board", merged.ClassId);
        Assert.IsFalse(merged.Mounted);
        Assert.AreEqual("Item0=sword, Item1=shield, Item2=, Item3=, Horse=, HorseHarness=, Head=helm, Body=mail", Slots(merged));
    }

    [TestMethod]
    public void AnArmourSet_ReplacesTheCavalryKitsArmour_AndNeverItsHorse()
    {
        var merged = LoadoutMerge.Merge(Rider(), new[] { Armour("a", S("Head", "heavy_helm"), S("Body", "heavy_mail")) })!;

        Assert.IsTrue(merged.Mounted);
        Assert.AreEqual("Item0=spear, Item1=rider_shield, Item2=rider_sword, Item3=, Head=heavy_helm, Body=heavy_mail, Horse=charger, HorseHarness=barding", Slots(merged));
    }

    [TestMethod]
    public void ALaterSet_WinsASlot_InPurchaseOrder()
    {
        var first = Armour("a", S("Head", "helm_a"), S("Body", "mail_a"));
        var second = Armour("b", S("Head", "helm_b"));

        Assert.AreEqual("helm_b", Item(LoadoutMerge.Merge(Sword(), new[] { first, second }), "Head"));
        Assert.AreEqual("mail_a", Item(LoadoutMerge.Merge(Sword(), new[] { first, second }), "Body"), "a slot the later set does not name stays the earlier set's");
        Assert.AreEqual("helm_a", Item(LoadoutMerge.Merge(Sword(), new[] { second, first }), "Head"), "bought the other way round, the other set wins");
    }

    [TestMethod]
    public void AWeaponsSet_ReplacesAllFourWeaponSlots_ClearingTheOnesItDoesNotName_SoNoKitShieldStaysBesideAPolearm()
    {
        var merged = LoadoutMerge.Merge(Sword(), new[] { Weapons("w", new[] { "sword_and_board" }, S("Item0", "polearm")) })!;

        Assert.AreEqual("polearm", Item(merged, "Item0"));
        Assert.AreEqual("", Item(merged, "Item1"), "the kit's shield is cleared");
        Assert.AreEqual("", Item(merged, "Item2"));
        Assert.AreEqual("", Item(merged, "Item3"));
    }

    [TestMethod]
    public void AWeaponsSet_ClearsASlotTheKitNeverNamed_ByAddingAnEmptyEntry()
    {
        var kit = new LdLoadout("two_handed", false, new[] { S("Item0", "axe"), S("Horse", ""), S("HorseHarness", "") });

        var merged = LoadoutMerge.Merge(kit, new[] { Weapons("w", new[] { "two_handed" }, S("Item0", "bow"), S("Item1", "arrows")) })!;

        Assert.AreEqual("Item0=bow, Horse=, HorseHarness=, Item1=arrows, Item2=, Item3=", Slots(merged));
    }

    [TestMethod]
    public void AWeaponsSet_ReplacesTheClassesWeapons_ButNotItsArmour()
    {
        var merged = LoadoutMerge.Merge(Rider(), new[] { Weapons("w", new[] { "cavalry" }, S("Item0", "lance")) })!;

        Assert.AreEqual("Item0=lance, Item1=, Item2=, Item3=, Head=rider_helm, Body=rider_armour, Horse=charger, HorseHarness=barding", Slots(merged));
    }

    [TestMethod]
    public void AWeaponsSet_ForAClassItDoesNotList_IsSkipped()
    {
        var kit = Sword();

        var merged = LoadoutMerge.Merge(kit, new[] { Weapons("bow", new[] { "archer" }, S("Item0", "bow")), Armour("a", S("Head", "helm")) })!;

        Assert.AreEqual("sword", Item(merged, "Item0"));
        Assert.AreEqual("shield", Item(merged, "Item1"));
        Assert.AreEqual("helm", Item(merged, "Head"), "the other set still applies");
    }

    [TestMethod]
    public void AnArmourSet_ThatListsClasses_IsSkippedForAnotherClass()
    {
        var set = new GearSet("a", "a", "", GearKind.Armour, new[] { "cavalry" }, new[] { S("Head", "helm") }, 100);

        Assert.AreEqual(Show(Sword()), Show(LoadoutMerge.Merge(Sword(), new[] { set })));
    }

    [TestMethod]
    public void AHorseSlot_NeverComesFromASet()
    {
        var sneaky = Armour("a", S("Head", "helm"), S("Horse", "other_horse"), S("HorseHarness", "other_barding"));

        var rider = LoadoutMerge.Merge(Rider(), new[] { sneaky })!;
        var foot = LoadoutMerge.Merge(Sword(), new[] { sneaky })!;

        Assert.AreEqual("charger", Item(rider, "Horse"));
        Assert.AreEqual("barding", Item(rider, "HorseHarness"));
        Assert.AreEqual("", Item(foot, "Horse"), "a foot class still clears both horse slots");
        Assert.AreEqual("", Item(foot, "HorseHarness"));
        Assert.AreEqual("helm", Item(foot, "Head"));
        Assert.AreEqual(2, foot.Slots.Count(x => x.Slot.StartsWith("Horse")), "no horse slot was added");
    }

    [TestMethod]
    public void TheClassKitAndTheSets_AreNotChanged()
    {
        var kit = Rider();
        var before = Show(kit);
        var set = Armour("a", S("Head", "helm"));

        LoadoutMerge.Merge(kit, new[] { set });

        Assert.AreEqual(before, Show(kit));
        Assert.AreEqual(1, set.Slots.Count);
    }

    private static string Slots(LdLoadout? loadout) => string.Join(", ", loadout!.Slots.Select(x => x.Slot + "=" + x.ItemId));

    private static string Item(LdLoadout? loadout, string slot) => loadout!.Slots.Single(x => x.Slot == slot).ItemId;
}
