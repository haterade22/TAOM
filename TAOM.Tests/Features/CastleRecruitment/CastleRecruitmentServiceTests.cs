using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CulturalFeats;
using TAOM.Features.TroopProgression;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.CastleRecruitment;

[TestClass]
public class CastleRecruitmentServiceTests
{
    private ICastleRecruitmentSettingsProvider _settings = null!;
    private IWarEffectService _war = null!;
    private IModLogger _logger = null!;
    private OutcomeLog _outcomes = null!;
    private CastleRecruitmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICastleRecruitmentSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.IsAiEnabled.Returns(true);
        _settings.NotablesPerCastle.Returns(3);
        _war = Substitute.For<IWarEffectService>();
        _war.GetMultiplier(Arg.Any<string?>(), Arg.Any<WarEffectKind>()).Returns(1f);
        // The real outcome log over a fake logger, so the tests read the exact [Check] lines the reader reads.
        _logger = Substitute.For<IModLogger>();
        _outcomes = new OutcomeLog(_logger);
        // The real production service: with no culture it runs no engine code, so this class needs no game.
        _sut = new CastleRecruitmentService(
            _settings, new VolunteerProductionService(Substitute.For<ICulturalFeatsService>(), _war), _outcomes);
    }

    // --- BlocksAiRecruitAtCastle (#918) ---

    [TestMethod]
    public void BlocksAiRecruitAtCastle_NonCastleSellerAiOff_False()
    {
        _settings.IsAiEnabled.Returns(false);
        Assert.IsFalse(_sut.BlocksAiRecruitAtCastle(sellerInCastle: false, buyerIsPlayer: false));
    }

    [TestMethod]
    public void BlocksAiRecruitAtCastle_PlayerBuyerAiOff_False()
    {
        _settings.IsAiEnabled.Returns(false);
        Assert.IsFalse(_sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: true));
    }

    [TestMethod]
    public void BlocksAiRecruitAtCastle_AiBuyerAtCastleAiOff_True()
    {
        _settings.IsAiEnabled.Returns(false);
        Assert.IsTrue(_sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false));
    }

    [TestMethod]
    public void BlocksAiRecruitAtCastle_AiBuyerAtCastleMasterOff_True()
    {
        _settings.IsEnabled.Returns(false);
        _settings.IsAiEnabled.Returns(true);
        Assert.IsTrue(_sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false));
    }

    [TestMethod]
    public void BlocksAiRecruitAtCastle_AiBuyerAtCastleBothOn_False()
    {
        Assert.IsFalse(_sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false));
    }

    // --- IsEnabled / IsAiEnabled gating ---

    [TestMethod]
    public void IsEnabled_MasterOn_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsEnabled);
    }

    [TestMethod]
    public void IsEnabled_MasterOff_ReturnsFalse()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOnAiOn_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsAiEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOffAiOn_ReturnsFalse()
    {
        // Master gate must dominate: AI castle recruitment off when the feature is off.
        _settings.IsEnabled.Returns(false);
        _settings.IsAiEnabled.Returns(true);
        Assert.IsFalse(_sut.IsAiEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOnAiOff_ReturnsFalse()
    {
        _settings.IsAiEnabled.Returns(false);
        Assert.IsFalse(_sut.IsAiEnabled);
    }

    // --- GetOccupationTargets distribution (round-robin over GangLeader/Headman/Merchant/Artisan) ---

    [TestMethod]
    public void GetOccupationTargets_Three_OneEachOfFirstThree()
    {
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.IsFalse(t.ContainsKey(CastleNotableOccupation.Artisan));
        Assert.AreEqual(3, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_One_OnlyGangLeader()
    {
        _settings.NotablesPerCastle.Returns(1);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Four_OneEach()
    {
        _settings.NotablesPerCastle.Returns(4);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Artisan]);
        Assert.AreEqual(4, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Five_WrapsToSecondGangLeader()
    {
        _settings.NotablesPerCastle.Returns(5);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(2, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Artisan]);
        Assert.AreEqual(5, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Zero_Empty()
    {
        _settings.NotablesPerCastle.Returns(0);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(0, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Negative_TreatedAsZero()
    {
        _settings.NotablesPerCastle.Returns(-3);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(0, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_NeverContainsRuralNotable()
    {
        // RuralNotable would NRE in vanilla GetBasicVolunteer for a castle notable.
        _settings.NotablesPerCastle.Returns(5);
        var t = _sut.GetOccupationTargets();
        // The enum has no RuralNotable; this asserts the distribution only uses castle-safe slots.
        Assert.IsTrue(t.Count <= 4);
    }

    // --- GetSlotProductionProbability ---

    [TestMethod]
    public void GetSlotProductionProbability_Slot0_PositiveAndBelowOne()
    {
        var p = _sut.GetSlotProductionProbability(0, null, false);
        Assert.IsTrue(p > 0f && p < 1f, $"expected (0,1), got {p}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_MonotonicallyDecreasing()
    {
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(_sut.GetSlotProductionProbability(i, null, false) > _sut.GetSlotProductionProbability(i + 1, null, false),
                $"slot {i} should exceed slot {i + 1}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_NegativeIndex_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.GetSlotProductionProbability(-1, null, false), 0.0001f);
    }

    [TestMethod]
    public void GetSlotProductionProbability_AllSlotsWithinUnitInterval()
    {
        for (int i = 0; i < 6; i++)
        {
            var p = _sut.GetSlotProductionProbability(i, null, false);
            Assert.IsTrue(p >= 0f && p <= 1f, $"slot {i} probability {p} outside [0,1]");
        }
    }

    [TestMethod]
    public void GetSlotProductionProbability_NoKingdom_IsTheFixedCurve()
    {
        for (int i = 0; i < 6; i++)
            Assert.AreEqual(Curve(i), _sut.GetSlotProductionProbability(i, null, false), 1e-6f, $"slot {i}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_KingdomWithAVolunteerEffect_ScalesTheCurve()
    {
        _war.GetMultiplier("k1", WarEffectKind.VolunteerRate).Returns(1.2f);

        for (int i = 0; i < 6; i++)
            Assert.AreEqual(Curve(i) * 1.2f, _sut.GetSlotProductionProbability(i, "k1", false), 1e-6f, $"slot {i}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_PlayerClanCastle_IgnoresTheVolunteerEffect()
    {
        _war.GetMultiplier("k1", WarEffectKind.VolunteerRate).Returns(1.2f);

        Assert.AreEqual(Curve(0), _sut.GetSlotProductionProbability(0, "k1", true), 1e-6f);
    }

    [TestMethod]
    public void GetSlotProductionProbability_NegativeIndex_AsksNoMultiplier()
    {
        _sut.GetSlotProductionProbability(-1, "k1", false);

        _war.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    // --- The #918 in-game check: the blocked counter, the recruit classifier and the daily line ---

    private const string AiOff = "[Check] id=taom.castle-recruit.ai-off ";

    [TestMethod]
    public void BlocksAiRecruitAtCastle_True_CountsOneBlockedQuery()
    {
        _settings.IsAiEnabled.Returns(false);

        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        Assert.AreEqual(1, Blocked.Passes);
    }

    [TestMethod]
    public void BlocksAiRecruitAtCastle_False_CountsNothing()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: false, buyerIsPlayer: false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: true);
        _settings.IsAiEnabled.Returns(true);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        Assert.AreEqual(0, Blocked.Passes);
    }

    [TestMethod]
    [DataRow(false, true, false, false, false, DisplayName = "not a castle")]
    [DataRow(true, false, false, false, false, DisplayName = "no notable: a mercenary or prisoner")]
    [DataRow(true, true, true, false, false, DisplayName = "no recruiter: a garrison")]
    [DataRow(true, true, false, true, false, DisplayName = "the main hero")]
    [DataRow(true, true, false, false, true, DisplayName = "an AI lord")]
    public void IsAiCastleRecruit_TruthTable(bool isCastle, bool hasSource, bool recruiterIsNull, bool recruiterIsPlayer,
        bool expected)
    {
        Assert.AreEqual(expected, _sut.IsAiCastleRecruit(isCastle, hasSource, recruiterIsNull, recruiterIsPlayer));
    }

    [TestMethod]
    public void ReportCastleRecruitDay_AiOffNoAiRecruit_WritesPassWithTheDayCounts()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        _sut.ReportCastleRecruitDay(41);

        CollectionAssert.AreEqual(new[]
        {
            AiOff + "result=pass day=41 blocked=2 ai_recruits=0",
        }, CheckLines());
    }

    [TestMethod]
    public void NoteCastleRecruit_AiWhileOff_WritesFailAtOnce_AndTheDayFails()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        _sut.NoteCastleRecruit(42, "castle_x", "lord_x", "notable_x", "troop_x");
        var atOnce = CheckLines();
        _sut.ReportCastleRecruitDay(42);

        const string atOnceLine =
            AiOff + "result=fail day=42 event=ai-recruit settlement=castle_x recruiter=lord_x source=notable_x troop=troop_x";
        CollectionAssert.AreEqual(new[] { atOnceLine }, atOnce);
        CollectionAssert.AreEqual(new[]
        {
            atOnceLine,
            AiOff + "result=fail day=42 blocked=1 ai_recruits=1",
        }, CheckLines());
        Assert.AreEqual(1, Recruited.Fails);
        Assert.AreEqual(0, Recruited.Passes);
    }

    [TestMethod]
    public void NoteCastleRecruit_AiWhileOn_WritesNoLine_CountsAPass_AndTheDaySkips()
    {
        _sut.NoteCastleRecruit(43, "castle_x", "lord_x", "notable_x", "troop_x");
        Assert.AreEqual(0, CheckLines().Length);

        _sut.ReportCastleRecruitDay(43);

        Assert.AreEqual(1, Recruited.Passes);
        Assert.AreEqual(0, Recruited.Fails);
        CollectionAssert.AreEqual(new[] { AiOff + "result=skip reason=toggle-on day=43" }, CheckLines());
    }

    [TestMethod]
    public void ReportCastleRecruitDay_MasterOff_IsJudgedNotSkipped()
    {
        // The master toggle folds into IsAiEnabled, and the castle rule blocks AI lords then too.
        _settings.IsEnabled.Returns(false);
        _settings.IsAiEnabled.Returns(true);

        _sut.ReportCastleRecruitDay(5);

        CollectionAssert.AreEqual(new[]
        {
            AiOff + "result=pass day=5 blocked=0 ai_recruits=0",
        }, CheckLines());
    }

    [TestMethod]
    public void ReportCastleRecruitDay_StartsANewDay()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);
        _sut.NoteCastleRecruit(10, "castle_x", "lord_x", "notable_x", "troop_x");
        _sut.ReportCastleRecruitDay(10);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        _sut.ReportCastleRecruitDay(11);

        var lines = CheckLines();
        Assert.AreEqual(AiOff + "result=fail day=10 blocked=1 ai_recruits=1", lines[1]);
        Assert.AreEqual(AiOff + "result=pass day=11 blocked=2 ai_recruits=0", lines[2]);
    }

    [TestMethod]
    public void ReportCastleRecruitDay_SameDayTwice_WritesOneLine()
    {
        // The day that runs again after a load in the same process (the daily-tick-after-load trap).
        _settings.IsAiEnabled.Returns(false);

        _sut.ReportCastleRecruitDay(20);
        _sut.ReportCastleRecruitDay(20);

        Assert.AreEqual(1, CheckLines().Length);
    }

    [TestMethod]
    public void BeginCastleRecruitDays_DropsCountsFromBefore()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.BlocksAiRecruitAtCastle(sellerInCastle: true, buyerIsPlayer: false);

        _sut.BeginCastleRecruitDays();
        _sut.ReportCastleRecruitDay(2);

        CollectionAssert.AreEqual(new[]
        {
            AiOff + "result=pass day=2 blocked=0 ai_recruits=0",
        }, CheckLines());
    }

    [TestMethod]
    public void NoteCastleRecruit_NullIds_WriteNone()
    {
        _settings.IsAiEnabled.Returns(false);

        _sut.NoteCastleRecruit(4, null, null, null, null);

        CollectionAssert.AreEqual(new[]
        {
            AiOff + "result=fail day=4 event=ai-recruit settlement=none recruiter=none source=none troop=none",
        }, CheckLines());
    }

    [TestMethod]
    public void ToggleFlippedOnAfterAViolation_TheAtOnceFailStands_AndTheDaySkips()
    {
        _settings.IsAiEnabled.Returns(false);
        _sut.NoteCastleRecruit(6, "castle_x", "lord_x", "notable_x", "troop_x");
        _settings.IsAiEnabled.Returns(true);

        _sut.ReportCastleRecruitDay(6);

        CollectionAssert.AreEqual(new[]
        {
            AiOff + "result=fail day=6 event=ai-recruit settlement=castle_x recruiter=lord_x source=notable_x troop=troop_x",
            AiOff + "result=skip reason=toggle-on day=6",
        }, CheckLines());
    }

    private OutcomeCounter Blocked => _outcomes.Counter("taom.castle-recruit.ai-blocked");

    private OutcomeCounter Recruited => _outcomes.Counter("taom.castle-recruit.ai-recruited");

    private string[] CheckLines() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
        .Select(c => (string)c.GetArguments()[0]!)
        .Where(line => line.StartsWith("[Check]", System.StringComparison.Ordinal))
        .ToArray();

    private static float Curve(int slot) => 0.75f * (float)System.Math.Pow(0.85, slot + 1);

    private static int Sum(System.Collections.Generic.IReadOnlyDictionary<CastleNotableOccupation, int> d)
    {
        int s = 0;
        foreach (var kvp in d)
            s += kvp.Value;
        return s;
    }
}
