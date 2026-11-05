using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>boon</c> and <c>reroll</c> verdicts of #1079 (phase2-plan.md section 6): three distinct eligible cards, the pick applied (read back from the numbers it
/// changes, recomputed here and not through <c>RunModifiers</c>), and a reroll that spent one from the bank and left out the cards just shown when the pool allowed.
/// </summary>
[TestClass]
public class LongDefeatBoonChecksTests
{
    private static BoonRow Boon(string id, params (string Key, float Value)[] effects) =>
        new(id, BoonCategory.WarChest, "{=K}" + id, "{=T}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static readonly BoonRow Gold = Boon("gold", ("gold", 300f));
    private static readonly BoonRow Wains = Boon("wains", ("packBonus", 3f), ("garrisonCap", 30f)) with { Drawback = new[] { new BoonEffect("pricePercent", 10f) }, DrawbackText = "{=D}d" };

    private static BoonPickRecord Record(string taken, params string[][] draws)
    {
        var record = new BoonPickRecord(1, 3) { Taken = taken, TakenBeforeWave = 4 };
        foreach (var ids in draws)
            record.Draws.Add(new BoonDraw(ids, 12, 12, 0));
        return record;
    }

    [TestMethod]
    public void Pick_ThreeDistinctCards_TheTakenOneAmongThem_AndItsGoldPaid_Passes()
    {
        var record = Record("gold", new[] { "a", "b", "gold" });

        var verdict = LongDefeatBoonChecks.Pick(record, new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 300);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "p=1 cards=3 eligible=12 offered=a,b,gold taken=gold ");
        StringAssert.Contains(verdict.Detail, "gold=300");
    }

    [TestMethod]
    public void Pick_TheFortressAndWarChestNumbersMustMatchWhatTheBoonsOwnedImply()
    {
        var record = Record("wains", new[] { "a", "b", "wains" });
        var rules = RunRules.Default with { GarrisonCap = 250, PackBonus = 3 };
        var factors = new GoldFactors(1f, 1f, 1f, 1.1f);

        var right = LongDefeatBoonChecks.Pick(record, new[] { Wains }, RunRules.Default, rules, factors, 0);
        var noPackBonus = LongDefeatBoonChecks.Pick(record, new[] { Wains }, RunRules.Default, rules with { PackBonus = 0 }, factors, 0);
        var noPrice = LongDefeatBoonChecks.Pick(record, new[] { Wains }, RunRules.Default, rules, GoldFactors.One, 0);

        Assert.AreEqual(CheckResult.Pass, right.Result, right.Detail);
        Assert.AreEqual(CheckResult.Fail, noPackBonus.Result, "the pack bonus never reached the rules");
        Assert.AreEqual(CheckResult.Fail, noPrice.Result, "the drawback's price factor never reached the gold factors");
        StringAssert.Contains(noPackBonus.Detail, "applied=false");
    }

    [TestMethod]
    public void Pick_FailsOnAGoldBoonThatPaidNothing()
    {
        var verdict = LongDefeatBoonChecks.Pick(Record("gold", new[] { "a", "b", "gold" }), new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 0);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
    }

    [TestMethod]
    public void Pick_FailsOnARepeatedCard_AShortOffer_AndATakenCardThatWasNeverShown()
    {
        var repeated = LongDefeatBoonChecks.Pick(Record("gold", new[] { "a", "a", "gold" }), new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 300);
        var shortOffer = LongDefeatBoonChecks.Pick(Record("gold", new[] { "a", "gold" }), new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 300);
        var unseen = LongDefeatBoonChecks.Pick(Record("gold", new[] { "a", "b", "c" }), new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 300);

        Assert.AreEqual(CheckResult.Fail, repeated.Result);
        StringAssert.Contains(repeated.Detail, "distinct=false");
        Assert.AreEqual(CheckResult.Fail, shortOffer.Result, "twelve rows were eligible and only two were shown");
        Assert.AreEqual(CheckResult.Fail, unseen.Result);
    }

    [TestMethod]
    public void Pick_AShortPool_NeedsOnlyWhatIsLeft()
    {
        var record = new BoonPickRecord(4, 12) { Taken = "gold", TakenBeforeWave = 13 };
        record.Draws.Add(new BoonDraw(new[] { "a", "gold" }, 2, 2, 0));

        var verdict = LongDefeatBoonChecks.Pick(record, new[] { Gold }, RunRules.Default, RunRules.Default, GoldFactors.One, 300);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
    }

    [TestMethod]
    public void Reroll_SpentOneAndShowedNewCards_Passes()
    {
        var record = new BoonPickRecord(1, 3);
        record.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 12, 12, 2));
        record.Draws.Add(new BoonDraw(new[] { "d", "e", "f" }, 12, 9, 1) { BankBefore = 2 });

        var verdict = LongDefeatBoonChecks.Reroll(record, 1);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "p=1 draw=1 bank=2>1 cards=3 offered=d,e,f repeated=0");
    }

    [TestMethod]
    public void Reroll_Fails_WhenTheBankDidNotFall_OrACardRepeatedWithRoomToAvoidIt()
    {
        var bank = new BoonPickRecord(1, 3);
        bank.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 12, 12, 2));
        bank.Draws.Add(new BoonDraw(new[] { "d", "e", "f" }, 12, 9, 2) { BankBefore = 2 });
        var repeat = new BoonPickRecord(1, 3);
        repeat.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 12, 12, 2));
        repeat.Draws.Add(new BoonDraw(new[] { "a", "e", "f" }, 12, 9, 1) { BankBefore = 2 });

        Assert.AreEqual(CheckResult.Fail, LongDefeatBoonChecks.Reroll(bank, 1).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatBoonChecks.Reroll(repeat, 1).Result);
    }

    [TestMethod]
    public void Reroll_ARepeatIsFine_WhenFewerThanThreeOthersWereLeft()
    {
        var record = new BoonPickRecord(1, 3);
        record.Draws.Add(new BoonDraw(new[] { "a", "b", "c" }, 4, 4, 1));
        record.Draws.Add(new BoonDraw(new[] { "d", "c", "a" }, 4, 1, 0) { BankBefore = 1 });

        var verdict = LongDefeatBoonChecks.Reroll(record, 1);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "repeated=2");
    }
}
