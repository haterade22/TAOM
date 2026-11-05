using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The boon deck (phase2-plan.md section 2, "Boons"): a pick owed at every third cleared wave, a reroll earned at every fifth with a bank of three,
/// three weighted cards a draw from <see cref="RunRandom"/> stream 2, and the reroll's exclusion rule. The pinned draws below were computed by the
/// Python replay in <c>tools/run_long_defeat.py</c> (the same SplitMix64 walk), so the two implementations cannot drift apart.
/// </summary>
[TestClass]
public class BoonDeckTests
{
    // Seven ordinary rows then two Legend rows, in file order. Legend weighs 1 against 10.
    private static BoonRow Row(string id, BoonCategory category = BoonCategory.WarChest, string classId = "") =>
        new(id, category, "{=K_" + id + "}" + id, "{=K_" + id + "_Text}text", new[] { new BoonEffect(BoonKeys.BountyPercent, 10f) }) { ClassId = classId };

    private static BoonPlan Pool(params BoonRow[] rows) => new(rows, 1);

    private static BoonPlan Nine() => Pool(
        Row("a"), Row("b"), Row("c"), Row("d"), Row("e"), Row("f"), Row("g"),
        Row("L1", BoonCategory.Legend), Row("L2", BoonCategory.Legend));

    private static string[] Ids(IEnumerable<BoonRow> rows) => rows.Select(r => r.Id).ToArray();

    private static BoonDeck Deck(BoonPlan? plan = null, ulong seed = 1, int rerolls = 0, string classId = "", params BoonCategory[] troops) =>
        new(plan ?? Nine(), seed, rerolls, classId, troops);

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(2, 0)]
    [DataRow(3, 1)]
    [DataRow(5, 1)]
    [DataRow(6, 2)]
    [DataRow(9, 3)]
    public void APickIsOwedAtEveryThirdClear(int clears, int picks)
    {
        var deck = Deck();
        for (var n = 1; n <= clears; n++)
            deck.Cleared(n, n);

        Assert.AreEqual(picks, deck.Picks.Count + deck.Owed, "picks owed or opened");
    }

    [TestMethod]
    public void OnlyOneOfferIsOpenAtATime_AndTheNextOpensWhenThatOneIsTaken()
    {
        var deck = Deck();
        for (var n = 1; n <= 6; n++)
            deck.Cleared(n, n);

        Assert.AreEqual(1, deck.Picks.Count, "the second pick waits behind the open one");
        Assert.AreEqual(1, deck.Owed);
        var taken = deck.Offer[0].Id;

        Assert.IsTrue(deck.TryPick(taken, 6, 7));

        Assert.AreEqual(2, deck.Picks.Count, "taking the first opens the second at once");
        Assert.AreEqual(0, deck.Owed);
        Assert.AreEqual(6, deck.Picks[1].Wave);
        CollectionAssert.DoesNotContain(Ids(deck.Offer), taken, "a boon owned is never offered again");
    }

    [TestMethod]
    [DataRow(4, 0)]
    [DataRow(5, 1)]
    [DataRow(10, 2)]
    [DataRow(15, 3)]
    [DataRow(30, 3)]
    public void ARerollIsEarnedAtEveryFifthClear_AndTheBankHoldsThree(int clears, int banked)
    {
        var deck = Deck();
        for (var n = 1; n <= clears; n++)
            deck.Cleared(n, n);

        Assert.AreEqual(banked, deck.Banked);
        Assert.AreEqual(banked, deck.Earned, "a reroll lost to the cap is never counted as earned");
    }

    [TestMethod]
    public void TheStartBank_IsSeededAndHeldToThree()
    {
        Assert.AreEqual(2, Deck(rerolls: 2).Banked);
        Assert.AreEqual(2, Deck(rerolls: 2).Earned, "a seeded reroll counts as earned, so the bank is always earned minus used");
        Assert.AreEqual(3, Deck(rerolls: 9).Banked);
    }

    [TestMethod]
    public void TheFirstOffer_ForSeedOne_IsThePinnedDraw()
    {
        var deck = Deck(seed: 1);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);

        CollectionAssert.AreEqual(new[] { "a", "f", "g" }, Ids(deck.Offer));
        Assert.AreEqual(9, deck.Picks[0].Draws[0].Eligible);
        Assert.AreEqual(3, deck.Picks[0].Wave, "offered at the clear of wave 3");
    }

    [TestMethod]
    public void TheFirstOffer_ForSeedSeven_IsThePinnedDraw()
    {
        var deck = Deck(seed: 7);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);

        CollectionAssert.AreEqual(new[] { "a", "e", "d" }, Ids(deck.Offer));
    }

    [TestMethod]
    public void ARerollDiscardsTheOffer_AndLeavesOutTheThreeJustShown_ThenTheNextOneLeavesOutTheLast()
    {
        var deck = Deck(seed: 1, rerolls: 2);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);

        Assert.IsTrue(deck.TryReroll());
        CollectionAssert.AreEqual(new[] { "b", "e", "L2" }, Ids(deck.Offer));
        Assert.IsTrue(deck.TryReroll());
        CollectionAssert.AreEqual(new[] { "f", "c", "L1" }, Ids(deck.Offer), "the first set is back in play, only the last three are out");
        Assert.AreEqual(0, deck.Banked);
        Assert.AreEqual(2, deck.Used);
        Assert.AreEqual(3, deck.Picks[0].Draws.Count, "the report lists every draw, rerolls included");
        Assert.AreEqual(1, deck.Picks[0].Draws[1].BankAfter);
        Assert.AreEqual(6, deck.Picks[0].Draws[1].Others, "nine eligible, three shown");
    }

    [TestMethod]
    public void ARerollEarnedWhileAPickIsStillOpen_IsRecordedAsSpendingOne_AndPassesTheCheck()
    {
        var deck = Deck(seed: 1);
        for (var n = 1; n <= 5; n++)
            deck.Cleared(n, n);

        Assert.AreEqual(1, deck.Banked, "earned at clear 5 while pick 1 is still open");
        Assert.IsTrue(deck.TryReroll());
        Assert.AreEqual(1, deck.Picks[0].Draws[1].BankBefore);
        Assert.AreEqual(0, deck.Picks[0].Draws[1].BankAfter);
        var verdict = TAOM.Features.LongDefeat.Diagnostics.LongDefeatBoonChecks.Reroll(deck.Picks[0], 1);
        Assert.AreEqual(TAOM.Features.LongDefeat.Diagnostics.CheckResult.Pass, verdict.Result, verdict.Detail);
    }

    [TestMethod]
    public void ARerollWithNoneBanked_IsRefused_AndSoIsOneWithNoOffer()
    {
        var deck = Deck(seed: 1);
        Assert.IsFalse(deck.TryReroll(), "no offer");
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);

        Assert.IsFalse(deck.TryReroll(), "no reroll banked");
        Assert.AreEqual(3, deck.Offer.Count);
        Assert.AreEqual(0, deck.Used);
    }

    [TestMethod]
    public void WithFewerThanThreeOthers_ARerollLeavesOutNone()
    {
        var deck = Deck(Pool(Row("a"), Row("b"), Row("c"), Row("d")), seed: 1, rerolls: 1);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);
        CollectionAssert.AreEqual(new[] { "a", "d", "c" }, Ids(deck.Offer));

        Assert.IsTrue(deck.TryReroll());

        CollectionAssert.AreEqual(new[] { "d", "c", "a" }, Ids(deck.Offer));
        Assert.AreEqual(1, deck.Picks[0].Draws[1].Others, "only one row was out of the last set, so nothing is left out");
    }

    [TestMethod]
    public void TheSecondPick_IsDrawnOverTheBoonsTakenBeforeIt_FromItsOwnStream()
    {
        var deck = Deck(seed: 1, rerolls: 1);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);
        deck.TryReroll();
        deck.TryPick("b", 3, 4);
        for (var n = 4; n <= 6; n++)
            deck.Cleared(n, n);

        CollectionAssert.AreEqual(new[] { "a", "g", "c" }, Ids(deck.Offer));
        Assert.AreEqual(2, deck.Picks[1].Pick);
        Assert.AreEqual(4, deck.Picks[0].TakenBeforeWave, "a boon taken in the intermission after wave 3 acts on wave 4");
        Assert.AreEqual("b", deck.Picks[0].Taken);
    }

    [TestMethod]
    public void APickOfACardNotOffered_IsRefused_AndLeavesTheOfferOpen()
    {
        var deck = Deck(seed: 1);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);

        Assert.IsFalse(deck.TryPick("b", 3, 4), "b was not among a, f, g");
        Assert.IsFalse(deck.TryPick("nope", 3, 4));
        Assert.AreEqual(3, deck.Offer.Count);
        Assert.AreEqual(0, deck.Owned.Count);
    }

    [TestMethod]
    public void FewerThanThreeEligible_OffersWhatIsLeft_AndNoneLapsesThePick()
    {
        var deck = Deck(Pool(Row("a"), Row("b")), seed: 1);
        for (var n = 1; n <= 3; n++)
            deck.Cleared(n, n);
        Assert.AreEqual(2, deck.Offer.Count, "offer what is left");
        deck.TryPick(deck.Offer[0].Id, 3, 4);
        deck.Cleared(4, 4);
        deck.Cleared(5, 5);
        deck.Cleared(6, 6);

        Assert.AreEqual(1, deck.Offer.Count, "one row left");
        deck.TryPick(deck.Offer[0].Id, 6, 7);
        for (var n = 7; n <= 9; n++)
            deck.Cleared(n, n);

        Assert.AreEqual(0, deck.Offer.Count);
        Assert.IsTrue(deck.Picks[2].Lapsed, "nothing left to offer: the pick lapses");
        Assert.AreEqual(0, deck.Owed);
    }

    [TestMethod]
    public void ARowOfAClassOrATroopCategory_IsOfferedOnlyWhenItsClassIsChosenOrItsTroopsAreInTheGarrison()
    {
        var plan = Pool(
            Row("war"), Row("fort", BoonCategory.Fortress),
            Row("archer_row", BoonCategory.Class, "archer"), Row("swords_row", BoonCategory.Class, "sword_and_board"),
            Row("foot", BoonCategory.Infantry), Row("bows", BoonCategory.Archers), Row("riders", BoonCategory.Cavalry));

        CollectionAssert.AreEqual(new[] { "war", "fort" }, Ids(Deck(plan).Eligible()));
        CollectionAssert.AreEqual(new[] { "war", "fort", "archer_row" }, Ids(Deck(plan, classId: "archer").Eligible()));
        CollectionAssert.AreEqual(new[] { "war", "fort", "foot", "bows" }, Ids(Deck(plan, troops: new[] { BoonCategory.Infantry, BoonCategory.Archers }).Eligible()));
        CollectionAssert.AreEqual(new[] { "war", "fort", "swords_row", "riders" },
            Ids(Deck(plan, classId: "sword_and_board", troops: new[] { BoonCategory.Cavalry }).Eligible()));
    }

    [TestMethod]
    public void ALegendRow_WeighsItsLegendWeight_AgainstTenForEveryOther()
    {
        var plan = new BoonPlan(new[] { Row("a"), Row("L1", BoonCategory.Legend) }, 40);

        Assert.AreEqual(10, plan.WeightOf(plan.Rows[0]));
        Assert.AreEqual(40, plan.WeightOf(plan.Rows[1]));
    }

    [TestMethod]
    public void ASecondDeckOnTheSameSeed_DrawsTheSameCards_WhateverTheRerollsBefore()
    {
        var one = Deck(seed: 1, rerolls: 1);
        var two = Deck(seed: 1, rerolls: 0);
        for (var n = 1; n <= 3; n++)
        {
            one.Cleared(n, n);
            two.Cleared(n, n);
        }

        one.TryReroll();

        CollectionAssert.AreEqual(Ids(two.Offer), Ids(one.Picks[0].Draws[0].Ids.Select(id => Nine().Find(id)!)), "draw 0 never depends on a later reroll");
    }
}
