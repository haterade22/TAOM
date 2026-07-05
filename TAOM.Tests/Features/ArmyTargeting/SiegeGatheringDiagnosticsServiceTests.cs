using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting.Diagnostics;

namespace TAOM.Tests.Features.ArmyTargeting;

[TestClass]
public class SiegeGatheringDiagnosticsServiceTests
{
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
    }

    private SiegeGatheringDiagnosticsService CreateSut() => new SiegeGatheringDiagnosticsService(_logger);

    private static SiegeGatheringFailureInfo ValidInfo() => new SiegeGatheringFailureInfo
    {
        ArmyName = "Yazdâr Army",
        LeaderName = "Yazdâr",
        ClanId = "clan_sh_8",
        KingdomId = "Shaghana",
        KingdomName = "Shaghâna",
        KingdomIsNull = false,
        FocusSettlementId = "town_SH_koth_rau",
        FocusSettlementName = "Kôth Rau",
        FocusCultureId = "shaghana",
        FocusFactionId = "Shaghana",
        FortificationsTotal = 6,
        FortificationsUnderSiege = 2,
        LeaderPartyX = 412.3f,
        LeaderPartyY = 180.7f,
        FocusX = 455.1f,
        FocusY = 205.9f,
        CampaignTimeText = "Winter 3, 1118"
    };

    // ---- Classify ----

    [TestMethod]
    public void Classify_KingdomIsNull_ReturnsKingdomNull()
    {
        var info = ValidInfo();
        info.KingdomIsNull = true;
        Assert.AreEqual(SiegeGatheringFailureReason.KingdomNull, CreateSut().Classify(info));
    }

    [TestMethod]
    public void Classify_NoFortifications_ReturnsNoFortifications()
    {
        var info = ValidInfo();
        info.FortificationsTotal = 0;
        info.FortificationsUnderSiege = 0;
        Assert.AreEqual(SiegeGatheringFailureReason.NoFortifications, CreateSut().Classify(info));
    }

    [TestMethod]
    public void Classify_AllUnderSiege_ReturnsAllFortificationsUnderSiege()
    {
        var info = ValidInfo();
        info.FortificationsTotal = 4;
        info.FortificationsUnderSiege = 4;
        Assert.AreEqual(SiegeGatheringFailureReason.AllFortificationsUnderSiege, CreateSut().Classify(info));
    }

    [TestMethod]
    public void Classify_FortificationsAvailable_ReturnsNoReachableFortification()
    {
        var info = ValidInfo();
        info.FortificationsTotal = 6;
        info.FortificationsUnderSiege = 2;
        Assert.AreEqual(SiegeGatheringFailureReason.NoReachableFortification, CreateSut().Classify(info));
    }

    [TestMethod]
    public void Classify_CountsUnavailable_ReturnsUnknown()
    {
        var info = ValidInfo();
        info.FortificationsTotal = SiegeGatheringFailureInfo.CountUnavailable;
        info.FortificationsUnderSiege = SiegeGatheringFailureInfo.CountUnavailable;
        Assert.AreEqual(SiegeGatheringFailureReason.Unknown, CreateSut().Classify(info));
    }

    [TestMethod]
    public void Classify_NullInfo_ReturnsUnknown()
    {
        Assert.AreEqual(SiegeGatheringFailureReason.Unknown, CreateSut().Classify(null));
    }

    // ---- Record: dedup + level routing ----

    [TestMethod]
    public void Record_FirstOccurrence_LogsWarningOnce()
    {
        CreateSut().Record(ValidInfo());

        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void Record_SameSiegeTwice_LogsWarningOnceThenDebug()
    {
        var sut = CreateSut();

        sut.Record(ValidInfo());
        sut.Record(ValidInfo());

        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void Record_DistinctSieges_LogsWarningPerSiege()
    {
        var sut = CreateSut();

        var a = ValidInfo();
        var b = ValidInfo();
        b.FocusSettlementId = "town_SH_other";

        sut.Record(a);
        sut.Record(b);

        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Record_NullInfo_DoesNotLog()
    {
        CreateSut().Record(null);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    // ---- Format ----

    [TestMethod]
    public void Format_ValidInfo_IncludesKeyDiagnosticFields()
    {
        var info = ValidInfo();
        string line = CreateSut().Format(info, SiegeGatheringFailureReason.NoReachableFortification);

        StringAssert.Contains(line, "[SiegeDiag]");
        StringAssert.Contains(line, "Yazdâr Army");
        StringAssert.Contains(line, "Shaghâna");
        StringAssert.Contains(line, "Kôth Rau");
        StringAssert.Contains(line, "town_SH_koth_rau");
        StringAssert.Contains(line, "NoReachableFortification");
        StringAssert.Contains(line, "6 total");
        StringAssert.Contains(line, "2 under siege");
    }

    [TestMethod]
    public void Format_CountsUnavailable_RendersNa()
    {
        var info = ValidInfo();
        info.FortificationsTotal = SiegeGatheringFailureInfo.CountUnavailable;
        info.FortificationsUnderSiege = SiegeGatheringFailureInfo.CountUnavailable;

        string line = CreateSut().Format(info, SiegeGatheringFailureReason.Unknown);

        StringAssert.Contains(line, "n/a total");
    }

    [TestMethod]
    public void Format_NaNPositions_RenderQuestionMark()
    {
        var info = ValidInfo();
        info.LeaderPartyX = float.NaN;
        info.LeaderPartyY = float.NaN;

        string line = CreateSut().Format(info, SiegeGatheringFailureReason.NoReachableFortification);

        StringAssert.Contains(line, "Leader@(?,?)");
    }

    [TestMethod]
    public void Format_NullOrBlankFields_RenderSafely()
    {
        var info = new SiegeGatheringFailureInfo
        {
            ArmyName = null,
            KingdomName = null,
            FocusSettlementName = "",
            KingdomIsNull = true
        };

        // Must not throw, and must substitute a placeholder for the missing fields.
        string line = CreateSut().Format(info, SiegeGatheringFailureReason.KingdomNull);

        StringAssert.Contains(line, SiegeGatheringFailureInfo.Unknown);
        StringAssert.Contains(line, "KingdomNull");
    }
}
