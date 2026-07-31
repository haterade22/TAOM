using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight.Cheats;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Pins the `taom.print_party_size` console report.
///
/// The 2026-07-11 rework moved the elite tax off the member COUNT and onto the party-size LIMIT, so
/// counts read raw everywhere and the limit deflates instead. That is invisible in-game: the player
/// sees "150/240" with no indication of which number the weight touched. This command prints the
/// whole chain — raw, weighted, true base limit, penalty, final limit.
///
/// The load-bearing requirement is that a `penalty=0` reading must say WHY. `ComputeSizePenalty`
/// returns 0 both when the party is light (nothing to tax) and when `baseLimit &lt; 2` — the guard
/// added after a NaN-poisoned `(int)ResultNumber` cast defeated the clamp. Those two zeroes mean
/// opposite things, and a report that renders them identically would hide exactly the bug the guard
/// exists to catch.
/// </summary>
[TestClass]
public class TroopWeightCheatsFormatTests
{
    [TestMethod]
    public void FormatPartySize_HeavyParty_RendersTheWholeChain()
    {
        // Arrange / Act
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Player party", rawCount: 214, weightedCount: 331,
            trueBaseLimit: 250, finalLimit: 133, weightEnabled: true);

        // Assert
        StringAssert.Contains(report, "raw=214");
        StringAssert.Contains(report, "weighted=331");
        StringAssert.Contains(report, "trueBase=250");
        StringAssert.Contains(report, "penalty=117");
        StringAssert.Contains(report, "finalLimit=133");
    }

    [TestMethod]
    public void FormatPartySize_LightParty_SaysThePenaltyIsZeroBecauseThePartyIsLight()
    {
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Player party", rawCount: 40, weightedCount: 40,
            trueBaseLimit: 120, finalLimit: 120, weightEnabled: true);

        StringAssert.Contains(report, "penalty=0");
        StringAssert.Contains(report, "party under weight");
        Assert.IsFalse(report.Contains("degenerate"));
    }

    /// <summary>
    /// The NaN-cast landing site. `ComputeSizePenalty` bails to 0 when `baseLimit &lt; 2`, which is how
    /// a poisoned limit presents. It must NOT read as "this party is light".
    /// </summary>
    [TestMethod]
    public void FormatPartySize_DegenerateBaseLimit_SaysDegenerateNotLight()
    {
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Player party", rawCount: 40, weightedCount: 90,
            trueBaseLimit: 1, finalLimit: 1, weightEnabled: true);

        StringAssert.Contains(report, "penalty=0");
        StringAssert.Contains(report, "degenerate");
        Assert.IsFalse(report.Contains("party under weight"));
    }

    /// <summary>
    /// `ComputeSizePenalty` clamps the penalty at `baseLimit - 1` so a party always keeps its leader.
    /// A clamped reading means the weight surplus exceeded the whole limit, which is worth seeing.
    /// </summary>
    [TestMethod]
    public void FormatPartySize_PenaltyClampedAtBaseLimitMinusOne_SaysClamped()
    {
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Player party", rawCount: 10, weightedCount: 500,
            trueBaseLimit: 20, finalLimit: 1, weightEnabled: true);

        StringAssert.Contains(report, "clamped");
    }

    /// <summary>
    /// With the feature off the whole chain should still print — reading the disabled-vs-enabled delta
    /// is the entire point of running this before and after an MCM toggle.
    /// </summary>
    [TestMethod]
    public void FormatPartySize_WeightDisabled_StillPrintsTheChainAndSaysDisabled()
    {
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Player party", rawCount: 214, weightedCount: 331,
            trueBaseLimit: 250, finalLimit: 250, weightEnabled: false);

        StringAssert.Contains(report, "raw=214");
        StringAssert.Contains(report, "EnableTroopWeight=False");
    }

    [TestMethod]
    public void FormatPartySize_IncludesThePartyName()
    {
        var report = TroopWeightCheats.FormatPartySize(
            partyName: "Warband of Dain", rawCount: 5, weightedCount: 5,
            trueBaseLimit: 50, finalLimit: 50, weightEnabled: true);

        StringAssert.Contains(report, "Warband of Dain");
    }
}
