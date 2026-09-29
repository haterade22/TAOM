using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The lord's gear ladder counts the enemies the player's hero strikes down in battle (#693). The mission
/// counts them (agent removals can arrive off the main thread, #634) and hands the total to the campaign,
/// which takes it once when the battle ends.
/// </summary>
[TestClass]
public class HeroKillTallyTests
{
    [TestMethod]
    public void Take_ReturnsWhatWasAdded_AndEmptiesTheTally()
    {
        var tally = new HeroKillTally();
        tally.Add(12);
        tally.Add(3);

        Assert.AreEqual(15, tally.Take());
        Assert.AreEqual(0, tally.Take());
    }

    [TestMethod]
    public void Add_NothingOrANegative_IsIgnored()
    {
        var tally = new HeroKillTally();
        tally.Add(0);
        tally.Add(-5);

        Assert.AreEqual(0, tally.Take());
    }

    [TestMethod]
    public void Add_FromManyThreads_LosesNothing()
    {
        var tally = new HeroKillTally();

        Parallel.For(0, 1000, _ => tally.Add(1));

        Assert.AreEqual(1000, tally.Take());
    }

    [TestMethod]
    [DataRow(true, false, false, true, "a kill counts")]
    [DataRow(false, true, true, true, "a knockout counts when the ladder counts knockouts")]
    [DataRow(false, true, false, false, "a knockout does not when it does not")]
    [DataRow(false, false, true, false, "routed or deleted is not struck down")]
    public void Counts_AKill_OrAKnockoutWhenTheLadderCountsThem(bool killed, bool knockedOut, bool countsKnockouts,
        bool expected, string because)
    {
        // Whose kill the counter checks before this (managed identity first, #634); whether a human enemy, only
        // after it and only for the hero's own kills.
        Assert.AreEqual(expected, HeroKillTally.Counts(killed, knockedOut, countsKnockouts), because);
    }
}
