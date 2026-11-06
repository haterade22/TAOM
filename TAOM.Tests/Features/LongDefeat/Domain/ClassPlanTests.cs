using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The classes of a run (phase2-plan.md section 2, "Classes"): which classes a fortress offers, the kit a class gives the lord (a foot class always clears the
/// horse slots), and the bot's pick, which is <c>RunRandom.For(seed, 3, 0, 0).Below(classes on offer)</c>.
/// </summary>
[TestClass]
public class ClassPlanTests
{
    private static RunClass Class(string id, bool mounted = false, string culture = "vlandia", params (string Slot, string Item)[] slots) =>
        new(id, "{=N_" + id + "}" + id, "{=T_" + id + "}text", mounted,
            new Dictionary<string, ClassKit>(StringComparer.OrdinalIgnoreCase) { [culture] = new ClassKit(slots.Select(s => new LdKitSlot(s.Slot, s.Item)).ToList()) });

    private static IReadOnlyList<RunClass> Three() => new[] { Class("sword"), Class("axe"), Class("bow") };

    [TestMethod]
    public void OnOffer_KeepsTheClassesWithAKitForTheCulture_InFileOrder_WithoutCase()
    {
        var plan = new ClassPlan(new[] { Class("a"), Class("b", culture: "gondor"), Class("c", culture: "VLANDIA") });

        CollectionAssert.AreEqual(new[] { "a", "c" }, plan.OnOffer("Vlandia").Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "b" }, plan.OnOffer("gondor").Select(c => c.Id).ToArray());
        Assert.AreEqual(0, plan.OnOffer("mordor").Count);
        Assert.AreEqual(0, ClassPlan.None.OnOffer("vlandia").Count);
    }

    [TestMethod]
    public void LoadoutFor_AFootClass_AppendsBothHorseSlotsCleared_AfterTheKit()
    {
        var foot = Class("sword", false, "vlandia", ("Item0", "sword_a"), ("Item1", "shield_a"));

        var loadout = foot.LoadoutFor("vlandia")!;

        Assert.AreEqual("sword", loadout.ClassId);
        Assert.IsFalse(loadout.Mounted);
        CollectionAssert.AreEqual(
            new[] { "Item0=sword_a", "Item1=shield_a", "Horse=", "HorseHarness=" },
            loadout.Slots.Select(s => s.Slot + "=" + s.ItemId).ToArray());
    }

    [TestMethod]
    public void LoadoutFor_AMountedClass_KeepsTheKitAsWritten()
    {
        var rider = Class("rider", true, "vlandia", ("Item0", "spear"), ("Horse", "horse_a"), ("HorseHarness", "harness_a"));

        var loadout = rider.LoadoutFor("vlandia")!;

        Assert.IsTrue(loadout.Mounted);
        CollectionAssert.AreEqual(new[] { "Item0=spear", "Horse=horse_a", "HorseHarness=harness_a" }, loadout.Slots.Select(s => s.Slot + "=" + s.ItemId).ToArray());
    }

    [TestMethod]
    public void LoadoutFor_ACultureWithNoKit_IsNull()
    {
        Assert.IsNull(Class("sword").LoadoutFor("gondor"));
    }

    [TestMethod]
    public void Choose_AnAskedClassOnOffer_IsTaken_WhoeverAsks()
    {
        var offer = Three();

        Assert.AreEqual("bow", ClassPlan.Choose(offer, "bow", 5UL, bot: false)!.Id);
        Assert.AreEqual("bow", ClassPlan.Choose(offer, "bow", 5UL, bot: true)!.Id, "an armed class beats the seed");
    }

    [TestMethod]
    public void Choose_NothingAsked_AndNoBot_TakesTheFirstClass()
    {
        Assert.AreEqual("sword", ClassPlan.Choose(Three(), null, 7UL, bot: false)!.Id);
    }

    [TestMethod]
    public void Choose_NothingAsked_AndTheBot_TakesTheSeedsDrawOverTheClassesOnOffer()
    {
        var offer = Three();
        var seen = new HashSet<string>();
        for (ulong seed = 0; seed < 40; seed++)
        {
            var chosen = ClassPlan.Choose(offer, null, seed, bot: true)!;
            Assert.AreEqual(offer[RunRandom.For(seed, RunRandom.ClassStream, 0, 0).Below(offer.Count)].Id, chosen.Id, "seed " + seed);
            seen.Add(chosen.Id);
        }

        Assert.AreEqual(3, seen.Count, "the draw reaches every class");
    }

    [DataTestMethod]
    [DataRow(0UL, "axe")]
    [DataRow(1UL, "sword")]
    [DataRow(2UL, "bow")]
    [DataRow(3UL, "bow")]
    [DataRow(4UL, "bow")]
    [DataRow(5UL, "sword")]
    [DataRow(6UL, "axe")]
    [DataRow(7UL, "axe")]
    public void Choose_TheBotsPick_IsPinned_ToTheSameVectorsAsThePythonReplay(ulong seed, string expected)
    {
        Assert.AreEqual(expected, ClassPlan.Choose(Three(), null, seed, bot: true)!.Id);
    }

    // The shipped offer since Slice 11: the three foot classes and Cavalry, so the bot's draw is Below(4). The same seeds and answers are pinned in test_run_long_defeat.py
    // (test_the_bots_class_is_pinned_to_the_games_vectors), which reads the order from classes.json.
    [DataTestMethod]
    [DataRow(0UL, "sword_and_board")]
    [DataRow(1UL, "archer")]
    [DataRow(2UL, "cavalry")]
    [DataRow(3UL, "archer")]
    [DataRow(4UL, "two_handed")]
    [DataRow(5UL, "cavalry")]
    [DataRow(6UL, "archer")]
    [DataRow(7UL, "archer")]
    public void Choose_TheBotsPick_OverTheShippedFour_IncludesCavalry_AndIsPinnedToThePythonReplay(ulong seed, string expected)
    {
        var shipped = new[] { Class("sword_and_board"), Class("two_handed"), Class("archer"), Class("cavalry", mounted: true) };

        Assert.AreEqual(expected, ClassPlan.Choose(shipped, null, seed, bot: true)!.Id);
    }

    [TestMethod]
    public void Choose_AnUnknownClass_FallsBackLikeNoRequest()
    {
        var offer = Three();

        Assert.AreEqual("sword", ClassPlan.Choose(offer, "cavalry", 7UL, bot: false)!.Id);
        Assert.AreEqual("axe", ClassPlan.Choose(offer, "cavalry", 7UL, bot: true)!.Id);
    }

    [TestMethod]
    public void Choose_NothingOnOffer_IsNull()
    {
        Assert.IsNull(ClassPlan.Choose(Array.Empty<RunClass>(), "bow", 1UL, bot: true));
    }

    [TestMethod]
    public void KitSlots_AreTheElevenNamesAKitMayUse_InTheEnginesOldXmlSpelling()
    {
        CollectionAssert.AreEqual(
            new[] { "Item0", "Item1", "Item2", "Item3", "Head", "Body", "Leg", "Gloves", "Cape", "Horse", "HorseHarness" },
            KitSlots.Names.ToArray());
        Assert.IsTrue(KitSlots.IsKnown("Head"));
        Assert.IsFalse(KitSlots.IsKnown("Weapon0"), "the XML names are Item0 to Item3");
        Assert.IsFalse(KitSlots.IsKnown("item0"), "case matters: the engine parses the name");
        Assert.IsTrue(KitSlots.IsMount("Horse") && KitSlots.IsMount("HorseHarness") && !KitSlots.IsMount("Cape"));
    }
}
