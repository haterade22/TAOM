using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>The end-of-match numbers that balance work reads: what each troop killed and lost, and each commander's gold.</summary>
[TestClass]
public class FordsTroopTallyTests
{
    [TestMethod]
    public void Lines_GiveEachTroopsKillsDeathsAndTheValueOfBoth_BestKillerFirst()
    {
        var tally = new FordsTroopTally();
        tally.Death("orc", "elf", 4);
        tally.Death("orc", "elf", 4);
        tally.Death("elf", "orc", 60);

        var lines = tally.Lines().ToList();

        CollectionAssert.AreEqual(new[]
        {
            "troop=orc kills=1 deaths=2 valueKilled=60 valueLost=8",
            "troop=elf kills=2 deaths=1 valueKilled=8 valueLost=60",
        }, lines);
    }

    [TestMethod]
    public void Death_WithNoKnownKiller_CountsTheDeath_UnderTheUnknownRow()
    {
        var tally = new FordsTroopTally();

        tally.Death("orc", null, 4);

        CollectionAssert.AreEquivalent(new[]
        {
            "troop=orc kills=0 deaths=1 valueKilled=0 valueLost=4",
            "troop=(unknown) kills=1 deaths=0 valueKilled=4 valueLost=0",
        }, tally.Lines().ToList());
    }

    [TestMethod]
    public void Ledger_AddsUpWhatASlotEarnedAndSpent_AndWhatMaxGoldCutOffIsNotEarned()
    {
        var ledger = new GoldLedger(FordsMatchConfig.Default.Economy with { StartingGold = 100, MaxGold = 150 }, new[] { 1 });

        ledger.Earn(1, 200);
        ledger.TrySpend(1, 30);
        ledger.TrySpend(1, 9999);

        Assert.AreEqual(50, ledger.Earned(1));
        Assert.AreEqual(30, ledger.Spent(1));
        Assert.AreEqual(120, ledger.Balance(1));
    }

    [TestMethod]
    public void GoldTotals_GiveEverySlotWithItsSide()
    {
        var match = new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = 1000 } }), Layout(), Factions(), Slots("slow"), new SeededFordsRandom(7));

        match.TryBuyUpgrade(WestSlot, "melee_damage");

        var west = match.GoldTotals().Single(t => t.Slot == WestSlot);
        Assert.AreEqual(MatchSide.West, west.Side);
        Assert.AreEqual(300, west.Spent);
        Assert.AreEqual(700, west.Left);
        Assert.AreEqual(MatchSide.East, match.GoldTotals().Single(t => t.Slot == EastSlot).Side);
    }
}
