using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle.Ledger;

/// <summary>
/// The exact [WarLedger] v=1 lines that tools/analyze_war_ledger.py parses. The contract is the shared
/// fixture tools/tests/fixtures/war_ledger_v1.txt: <see cref="Fixture_IsExactlyWhatTheFormatterWrites"/>
/// pins the formatter to it and test_analyze_war_ledger.py parses it, so a rename on either side fails a
/// test. A change here is a format change: bump the version and teach the analyzer.
/// </summary>
[TestClass]
public class WarLedgerFormatterTests
{
    private static KingdomWarSnapshot Gondor() => new KingdomWarSnapshot
    {
        Id = "empire_w",
        CultureId = "gondor",
        Towns = 3,
        Castles = 1,
        AtWar = true,
        HomeHeld = true,
        Strength = 1234.4f,
        PrisonerLords = 2,
    };

    [TestMethod]
    public void Kingdom_AllFieldsKnown_IsTheGoldenLine()
    {
        var line = WarLedgerFormatter.Kingdom("c1", 12, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, 1, 1.1f, 1.5f);

        Assert.AreEqual(
            "[WarLedger] v=1 t=kingdom cid=c1 day=12 phase=FullWar k=empire_w side=free ai=1 war=1 towns=3 castles=1 "
            + "pts=7 base=10 loss=0.300 cap=1 str=1234 pris=2 tier=1 vr=1.10 pe=1.50",
            line);
    }

    [TestMethod]
    public void Kingdom_NothingKnown_PrintsNaAndZeroes()
    {
        var k = new KingdomWarSnapshot { Id = "rebels", Towns = 0, Castles = 1, HomeHeld = null, Strength = 0f };

        var line = WarLedgerFormatter.Kingdom("c1", 0, WarPhase.Peace, k, FactionSide.Neutral, null, 0, 1f, 1f);

        Assert.AreEqual(
            "[WarLedger] v=1 t=kingdom cid=c1 day=0 phase=Peace k=rebels side=neutral ai=1 war=0 towns=0 castles=1 "
            + "pts=1 base=na loss=na cap=na str=0 pris=0 tier=0 vr=1.00 pe=1.00",
            line);
    }

    [TestMethod]
    public void Kingdom_PlayerRuled_WritesAiZero_AndAHomeLostWritesCapZero()
    {
        var k = Gondor();
        k.IsPlayerRuled = true;
        k.HomeHeld = false;

        var line = WarLedgerFormatter.Kingdom("c1", 3, WarPhase.IsengardWar, k, FactionSide.Evil, 7, 0, 1f, 1f);

        StringAssert.Contains(line, "phase=IsengardWar ");
        StringAssert.Contains(line, "side=evil ai=0 ");
        StringAssert.Contains(line, " cap=0 ");
    }

    [TestMethod]
    public void Kingdom_MoreLandThanTheBaseline_LossIsZeroNotNegative()
    {
        var line = WarLedgerFormatter.Kingdom("c1", 3, WarPhase.FullWar, Gondor(), FactionSide.Free, 4, 0, 1f, 1f);

        StringAssert.Contains(line, " base=4 loss=0.000 ");
    }

    [TestMethod]
    public void Kingdom_ABaselineOfZero_LossIsNaInsteadOfDividingByZero()
    {
        var line = WarLedgerFormatter.Kingdom("c1", 3, WarPhase.FullWar, Gondor(), FactionSide.Free, 0, 0, 1f, 1f);

        StringAssert.Contains(line, " base=0 loss=na ");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-5f)]
    public void Kingdom_StrengthThatIsNotAUsableNumber_PrintsMinusOne(float strength)
    {
        var k = Gondor();
        k.Strength = strength;

        var line = WarLedgerFormatter.Kingdom("c1", 1, WarPhase.FullWar, k, FactionSide.Free, 10, 0, 1f, 1f);

        StringAssert.Contains(line, " str=-1 ");
    }

    [DataTestMethod]
    [DataRow(1234.4f, "1234")]
    [DataRow(1234.5f, "1235")]
    [DataRow(1e12f, "2147483647")]
    public void Kingdom_StrengthIsRoundedToAnInt(float strength, string expected)
    {
        var k = Gondor();
        k.Strength = strength;

        var line = WarLedgerFormatter.Kingdom("c1", 1, WarPhase.FullWar, k, FactionSide.Free, 10, 0, 1f, 1f);

        StringAssert.Contains(line, " str=" + expected + " ");
    }

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(3, 2)]
    [DataRow(99, 2)]
    public void Kingdom_TierOutsideZeroToTwo_IsClamped(int tier, int expected)
    {
        var line = WarLedgerFormatter.Kingdom("c1", 1, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, tier, 1f, 1f);

        StringAssert.Contains(line, " tier=" + expected + " ");
    }

    [TestMethod]
    public void Kingdom_NonFiniteMultipliers_PrintOne()
    {
        var line = WarLedgerFormatter.Kingdom("c1", 1, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, 0, float.NaN, float.PositiveInfinity);

        StringAssert.Contains(line, " vr=1.00 pe=1.00");
    }

    [TestMethod]
    public void Kingdom_UnsafeIdText_CannotSplitOrForgeAToken()
    {
        var k = Gondor();
        k.Id = "bad id day=99";

        var line = WarLedgerFormatter.Kingdom("c 1", 12, WarPhase.FullWar, k, FactionSide.Free, 10, 0, 1f, 1f);

        StringAssert.Contains(line, "cid=c_1 ");
        StringAssert.Contains(line, " k=bad_id_day_99 ");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(line, " day=").Count);
    }

    [TestMethod]
    public void Kingdom_EmptyCampaignId_IsUnknown()
    {
        var line = WarLedgerFormatter.Kingdom("", 1, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, 0, 1f, 1f);

        StringAssert.Contains(line, " cid=unknown ");
    }

    [TestMethod]
    public void Kingdom_AnUndefinedPhase_IsUnknown()
    {
        var line = WarLedgerFormatter.Kingdom("c1", 1, (WarPhase)99, Gondor(), FactionSide.Free, 10, 0, 1f, 1f);

        StringAssert.Contains(line, " phase=unknown ");
    }

    [TestMethod]
    public void Side_IsTheGoldenLine()
    {
        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=12 side=evil pts=31 base=40 alive=3",
            WarLedgerFormatter.Side("c1", 12, FactionSide.Evil, 31, 40, 3));
        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=5 side=neutral pts=0 base=na alive=0",
            WarLedgerFormatter.Side("c1", 5, FactionSide.Neutral, 0, null, 0));
    }

    [TestMethod]
    public void Share_IsTheGoldenLine_AndNaWhenUnknown()
    {
        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=0.524", WarLedgerFormatter.Share("c1", 12, 11d / 21d));
        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=0.000", WarLedgerFormatter.Share("c1", 12, 0d));
        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=na", WarLedgerFormatter.Share("c1", 12, null));
        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=na", WarLedgerFormatter.Share("c1", 12, double.NaN));
    }

    [TestMethod]
    public void Events_AreTheGoldenLines()
    {
        Assert.AreEqual("[WarLedger] v=1 t=event cid=c1 day=40 ev=tier k=empire_w from=0 to=1 loss=0.250",
            WarLedgerFormatter.TierEvent("c1", 40, "empire_w", 0, 1, 0.25f));
        Assert.AreEqual("[WarLedger] v=1 t=event cid=c1 day=70 ev=destroyed k=vlandia",
            WarLedgerFormatter.DestroyedEvent("c1", 70, "vlandia"));
        Assert.AreEqual("[WarLedger] v=1 t=event cid=c1 day=71 ev=chronicle id=hornburg outcome=Held",
            WarLedgerFormatter.ChronicleEvent("c1", 71, "hornburg", "Held"));
    }

    [TestMethod]
    public void TierEvent_ClampsTheTiersAndKeepsAnInvalidLossFromBecomingNaN()
    {
        var line = WarLedgerFormatter.TierEvent("c1", 40, "k", -2, 7, float.NaN);

        Assert.AreEqual("[WarLedger] v=1 t=event cid=c1 day=40 ev=tier k=k from=0 to=2 loss=0.000", line);
    }

    [TestMethod]
    public void Fixture_IsExactlyWhatTheFormatterWrites()
    {
        var fixture = RepoPaths.ReadSource("tools/tests/fixtures/war_ledger_v1.txt")
            .Split('\n').Where(l => l.Length > 0).ToArray();
        var rebels = new KingdomWarSnapshot { Id = "rebels", Towns = 0, Castles = 1, HomeHeld = null, Strength = 0f };

        var written = new[]
        {
            WarLedgerFormatter.Kingdom("c1", 12, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, 1, 1.1f, 1.5f),
            WarLedgerFormatter.Kingdom("c1", 0, WarPhase.Peace, rebels, FactionSide.Neutral, null, 0, 1f, 1f),
            WarLedgerFormatter.Side("c1", 12, FactionSide.Evil, 31, 40, 3),
            WarLedgerFormatter.Side("c1", 5, FactionSide.Neutral, 0, null, 0),
            WarLedgerFormatter.Share("c1", 12, 11d / 21d),
            WarLedgerFormatter.Share("c1", 13, 0d),
            WarLedgerFormatter.Share("c1", 14, null),
            WarLedgerFormatter.TierEvent("c1", 40, "empire_w", 0, 1, 0.25f),
            WarLedgerFormatter.DestroyedEvent("c1", 70, "vlandia"),
            WarLedgerFormatter.ChronicleEvent("c1", 71, "hornburg", "Held"),
            WarLedgerFormatter.TierEvent("c1", 41, "k", -2, 7, float.NaN),
        };

        CollectionAssert.AreEqual(written, fixture);
    }

    [TestMethod]
    public void EveryLine_UnderAGermanThreadCulture_StillUsesInvariantNumbers()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var kingdom = WarLedgerFormatter.Kingdom("c1", 12, WarPhase.FullWar, Gondor(), FactionSide.Free, 10, 1, 1.1f, 1.5f);
            var share = WarLedgerFormatter.Share("c1", 12, 0.4567d);
            var tier = WarLedgerFormatter.TierEvent("c1", 12, "k", 0, 1, 0.25f);

            StringAssert.Contains(kingdom, " loss=0.300 ");
            StringAssert.Contains(kingdom, " vr=1.10 pe=1.50");
            Assert.IsFalse(kingdom.Contains(","), kingdom);
            Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=0.457", share);
            Assert.IsFalse(tier.Contains(","), tier);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }
}
