using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Ledger;

[TestClass]
public class WarLedgerServiceTests
{
    private IKingdomWarSnapshotAdapter _snapshots = null!;
    private WarBaselineService _baselines = null!;
    private RallyTierStore _tiers = null!;
    private IWarEffectService _effects = null!;
    private IWarOfTheRingService _wotr = null!;
    private IAlignmentService _alignment = null!;
    private IModLogger _logger = null!;
    private WarLedgerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        _snapshots.GetCampaignId().Returns("c1");
        _snapshots.GetElapsedDay().Returns(12);
        _logger = Substitute.For<IModLogger>();
        _baselines = new WarBaselineService();
        _tiers = new RallyTierStore();
        _effects = Substitute.For<IWarEffectService>();
        _effects.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        _wotr = Substitute.For<IWarOfTheRingService>();
        _wotr.CurrentPhase.Returns(WarPhase.FullWar);
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Neutral);
        _sut = new WarLedgerService(_snapshots, _baselines, _tiers, _effects, _wotr, _alignment, _logger);
    }

    private static KingdomWarSnapshot Kingdom(string id, string culture, int towns, int castles, bool war = true) =>
        new KingdomWarSnapshot { Id = id, CultureId = culture, Towns = towns, Castles = castles, AtWar = war, HomeHeld = true, Strength = 100f };

    private void Side(string kingdomId, FactionSide side) =>
        _alignment.ResolveSide(kingdomId, Arg.Any<string>()).Returns(side);

    [TestMethod]
    public void BuildDailyLines_Order_IsKingdomsThenThreeSidesThenTheShare()
    {
        Side("empire_w", FactionSide.Free);
        Side("empire_s", FactionSide.Evil);
        var kingdoms = new[] { Kingdom("empire_w", "gondor", 3, 1), Kingdom("empire_s", "mordor", 2, 2) };

        var lines = _sut.BuildDailyLines(kingdoms);

        Assert.AreEqual(6, lines.Count);
        StringAssert.Contains(lines[0], "t=kingdom cid=c1 day=12 phase=FullWar k=empire_w side=free");
        StringAssert.Contains(lines[1], "t=kingdom cid=c1 day=12 phase=FullWar k=empire_s side=evil");
        StringAssert.Contains(lines[2], "t=side cid=c1 day=12 side=free");
        StringAssert.Contains(lines[3], "t=side cid=c1 day=12 side=evil");
        StringAssert.Contains(lines[4], "t=side cid=c1 day=12 side=neutral");
        StringAssert.Contains(lines[5], "t=share cid=c1 day=12");
    }

    [TestMethod]
    public void BuildDailyLines_KingdomLine_CarriesBaselineTierMultipliersAndTheResolvedSide()
    {
        Side("empire_w", FactionSide.Free);
        var gondor = Kingdom("empire_w", "gondor", 3, 1);
        _baselines.EnsureBaselines(new[] { Kingdom("empire_w", "gondor", 4, 2) }); // 10 points at the start
        _tiers.SetTier("empire_w", 1);
        _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate).Returns(1.1f);
        _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape).Returns(1.5f);

        var lines = _sut.BuildDailyLines(new[] { gondor });

        Assert.AreEqual(
            "[WarLedger] v=1 t=kingdom cid=c1 day=12 phase=FullWar k=empire_w side=free ai=1 war=1 towns=3 castles=1 "
            + "pts=7 base=10 loss=0.300 cap=1 str=100 pris=0 tier=1 vr=1.10 pe=1.50",
            lines[0]);
        _alignment.Received().ResolveSide("empire_w", "gondor");
    }

    [TestMethod]
    public void BuildDailyLines_NoBaselineYet_PrintsNa()
    {
        var lines = _sut.BuildDailyLines(new[] { Kingdom("empire_w", "gondor", 3, 1) });

        StringAssert.Contains(lines[0], " base=na loss=na ");
    }

    [TestMethod]
    public void BuildDailyLines_TheWarPhaseComesFromTheWarOfTheRingService()
    {
        _wotr.CurrentPhase.Returns(WarPhase.IsengardWar);

        var lines = _sut.BuildDailyLines(new[] { Kingdom("empire_w", "gondor", 3, 1) });

        StringAssert.Contains(lines[0], " phase=IsengardWar ");
    }

    [TestMethod]
    public void BuildDailyLines_SideLines_SumPointsAndBaselinesAndCountKingdomsPerSide()
    {
        Side("empire_w", FactionSide.Free);
        Side("vlandia", FactionSide.Free);
        Side("empire_s", FactionSide.Evil);
        Side("sturgia", FactionSide.Neutral);
        _baselines.EnsureBaselines(new[]
        {
            Kingdom("empire_w", "gondor", 4, 2), Kingdom("vlandia", "rohan", 2, 0), Kingdom("empire_s", "mordor", 3, 2),
        });
        var kingdoms = new[]
        {
            Kingdom("empire_w", "gondor", 3, 1), Kingdom("vlandia", "rohan", 2, 0),
            Kingdom("empire_s", "mordor", 3, 2), Kingdom("sturgia", "dale", 1, 1),
        };

        var lines = _sut.BuildDailyLines(kingdoms);

        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=12 side=free pts=11 base=14 alive=2", lines[4]);
        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=12 side=evil pts=8 base=8 alive=1", lines[5]);
        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=12 side=neutral pts=3 base=na alive=1", lines[6]);
    }

    [TestMethod]
    public void BuildDailyLines_Share_IsFreeOverFreePlusEvil_AndIgnoresNeutral()
    {
        Side("empire_w", FactionSide.Free);
        Side("empire_s", FactionSide.Evil);
        Side("sturgia", FactionSide.Neutral);
        var kingdoms = new[]
        {
            Kingdom("empire_w", "gondor", 3, 1), Kingdom("empire_s", "mordor", 2, 2), Kingdom("sturgia", "dale", 20, 20),
        };

        var lines = _sut.BuildDailyLines(kingdoms);

        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=0.538", lines.Last());
    }

    [TestMethod]
    public void BuildDailyLines_BothSidesWithoutPoints_ShareIsNa()
    {
        Side("sturgia", FactionSide.Neutral);

        var lines = _sut.BuildDailyLines(new[] { Kingdom("sturgia", "dale", 1, 1) });

        Assert.AreEqual("[WarLedger] v=1 t=share cid=c1 day=12 share=na", lines.Last());
    }

    [TestMethod]
    public void BuildDailyLines_APlayerRuledKingdom_StillCountsForItsSideButIsNotAi()
    {
        Side("player_kingdom", FactionSide.Free);
        var player = Kingdom("player_kingdom", "gondor", 1, 0);
        player.IsPlayerRuled = true;

        var lines = _sut.BuildDailyLines(new[] { player });

        StringAssert.Contains(lines[0], " ai=0 ");
        StringAssert.Contains(lines[1], "side=free pts=2 ");
    }

    [TestMethod]
    public void BuildDailyLines_AStrengthThatIsNaN_IsWrittenAsMinusOne()
    {
        var kingdom = Kingdom("empire_w", "gondor", 3, 1);
        kingdom.Strength = float.NaN;

        var lines = _sut.BuildDailyLines(new[] { kingdom });

        StringAssert.Contains(lines[0], " str=-1 ");
    }

    [TestMethod]
    public void BuildDailyLines_ANullKingdomInTheList_SkipsIt()
    {
        var lines = _sut.BuildDailyLines(new[] { Kingdom("empire_w", "gondor", 3, 1), null!, Kingdom("empire_s", "mordor", 2, 2) });

        Assert.AreEqual(6, lines.Count, "two kingdom lines, three side lines and the share");
        Assert.AreEqual("[WarLedger] v=1 t=side cid=c1 day=12 side=neutral pts=13 base=na alive=2", lines[4]);
    }

    [TestMethod]
    public void BuildDailyLines_NoKingdoms_WritesNothing()
    {
        Assert.AreEqual(0, _sut.BuildDailyLines(new List<KingdomWarSnapshot>()).Count);
        Assert.AreEqual(0, _sut.BuildDailyLines(null!).Count);
    }

    [TestMethod]
    public void WriteDaily_LogsEveryLineAtInfo()
    {
        var kingdoms = new[] { Kingdom("empire_w", "gondor", 3, 1) };
        var expected = _sut.BuildDailyLines(kingdoms);

        _sut.WriteDaily(kingdoms);

        foreach (var line in expected)
            _logger.Received(1).LogInfo(line);
        _logger.Received(expected.Count).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogDestroyed_WritesTheEventLineWithTodaysDay()
    {
        _sut.LogDestroyed("vlandia");

        _logger.Received(1).LogInfo("[WarLedger] v=1 t=event cid=c1 day=12 ev=destroyed k=vlandia");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void LogDestroyed_WithoutAnId_WritesNothing(string? id)
    {
        _sut.LogDestroyed(id);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void LogTierChange_WritesTheEventLine()
    {
        _sut.LogTierChange("empire_w", 0, 1, 0.25f);

        _logger.Received(1).LogInfo("[WarLedger] v=1 t=event cid=c1 day=12 ev=tier k=empire_w from=0 to=1 loss=0.250");
    }

    [TestMethod]
    public void LogChronicleResolution_WritesTheEventLine()
    {
        _sut.LogChronicleResolution("hornburg", "Held");

        _logger.Received(1).LogInfo("[WarLedger] v=1 t=event cid=c1 day=12 ev=chronicle id=hornburg outcome=Held");
    }
}
