using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class FieldCommissionMeritServiceTests
{
    private ITroopRosterQueryAdapter _roster = null!;
    private IRaceManager _raceManager = null!;
    private IFieldCommissionConfigProvider _configProvider = null!;
    private IModLogger _logger = null!;
    private FieldCommissionConfig _config = null!;
    private FieldCommissionMeritService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _roster = Substitute.For<ITroopRosterQueryAdapter>();
        _raceManager = Substitute.For<IRaceManager>();
        _configProvider = Substitute.For<IFieldCommissionConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _config = new FieldCommissionConfig
        {
            MeritPerKill = 1,
            MeritThreshold = 8,
            AllowedRaceNames = new List<string> { "human", "dwarf", "elf" },
        };
        _configProvider.GetConfig().Returns(_config);

        _sut = new FieldCommissionMeritService(_roster, _raceManager, _configProvider, new TroopUpgradeGraph(), _logger);
    }

    private void SetupHumanTroop(string troopId, int level = 10, bool isHero = false, bool isPrisonGuard = false, int raceId = 0)
    {
        _roster.GetTroopInfo(troopId).Returns(new TroopInfo(troopId, troopId, isHero, isPrisonGuard, raceId, level));
        _raceManager.IsValidRaceId(raceId).Returns(true);
        _raceManager.GetRaceNameFromId(raceId).Returns("human");
    }

    // --- ComputeRatio / IsBattleEligible ---

    [TestMethod]
    public void ComputeRatio_ZeroEnemyHealthy_ClampsDenominatorToOne()
    {
        Assert.AreEqual(50f, _sut.ComputeRatio(50, 0), 0.001f);
    }

    [TestMethod]
    public void ComputeRatio_NormalValues_ReturnsPlayerOverEnemy()
    {
        Assert.AreEqual(0.5f, _sut.ComputeRatio(50, 100), 0.001f);
    }

    [TestMethod]
    public void IsBattleEligible_RatioBelowThreshold_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsBattleEligible(1.0f, 1.3f));
    }

    [TestMethod]
    public void IsBattleEligible_RatioAboveThreshold_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsBattleEligible(1.5f, 1.3f));
    }

    [TestMethod]
    public void IsBattleEligible_RatioEqualsThreshold_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsBattleEligible(1.3f, 1.3f));
    }

    [TestMethod]
    public void IsBattleEligible_NaNRatio_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsBattleEligible(float.NaN, 1.3f));
    }

    [TestMethod]
    public void IsBattleEligible_NaNThreshold_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsBattleEligible(1.0f, float.NaN));
    }

    // --- RegisterKill / BeginBattle / EndBattle ---

    [TestMethod]
    public void EndBattle_NotEligible_NoMeritAwardedEvenIfKillsRegistered()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(false);
        _sut.RegisterKill("troop_a");

        _sut.EndBattle(true);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void RegisterKill_TroopNotInPreBattleSnapshot_Ignored()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_b"); // never in the snapshot

        SetupHumanTroop("troop_b");
        _roster.GetTroopCount("troop_b").Returns(5);
        _sut.EndBattle(true);

        Assert.AreEqual(0, _sut.GetMerit("troop_b"));
    }

    [TestMethod]
    public void EndBattle_Lost_NoMeritAwarded()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");

        _sut.EndBattle(false);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_WonAndEligible_AwardsMeritPerKill()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");
        _sut.RegisterKill("troop_a");
        _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);

        _sut.EndBattle(true);

        Assert.AreEqual(3, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_MeritPerKillConfigTwo_DoublesAward()
    {
        _config.MeritPerKill = 2;
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);

        _sut.EndBattle(true);

        Assert.AreEqual(2, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_MeritReachesThreshold_QueuesOffer()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(1, offers.Count);
        Assert.AreEqual("troop_a", offers[0].TroopId);
        Assert.IsTrue(_sut.HasPendingOffers);
    }

    [TestMethod]
    public void EndBattle_MeritBelowThreshold_NoOfferQueuedButMeritBanked()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 3; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(0, offers.Count);
        Assert.AreEqual(3, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_OffersCappedByCurrentRosterCount()
    {
        // 24 merit / 8 threshold = 3 possible offers, but only 2 of that troop remain.
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 24; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(2);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(2, offers.Count);
    }

    [TestMethod]
    public void EndBattle_TroopNotPromotable_MeritBankedButNoOfferQueued()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");

        // A hero troop template — never promotable.
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", true, false, 0, 10));
        _roster.GetTroopCount("troop_a").Returns(5);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(0, offers.Count);
        Assert.AreEqual(8, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_AlwaysClearsInBattleTrackingRegardlessOfOutcome()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");
        _sut.EndBattle(false); // lost — kills discarded, tracking cleared

        // A LATER battle where troop_a is never re-snapshotted (BeginBattle(false)) must not see
        // a kill registered before tracking was cleared.
        _sut.BeginBattle(false);
        _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);
        _sut.EndBattle(true);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_TroopNoLongerInRosterWithDescendantPresent_TransfersKillsAndBankedMeritToDescendant()
    {
        // troop_a is fully upgraded away (0 left) but troop_b (its upgrade target) is present.
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 3; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(0); // none left
        _roster.GetUpgradeTargetIds("troop_a").Returns(new List<string> { "troop_b" });
        _roster.GetTroopCount("troop_b").Returns(5); // descendant present

        _sut.EndBattle(true);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
        Assert.AreEqual(3, _sut.GetMerit("troop_b"));
    }

    [TestMethod]
    public void EndBattle_TroopNoLongerInRosterWithNoDescendant_MeritDropped()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(0);
        _roster.GetUpgradeTargetIds("troop_a").Returns(new List<string>());

        _sut.EndBattle(true);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
        Assert.AreEqual(0, _sut.GetMerit("troop_b"));
    }

    [TestMethod]
    public void ConsolidateOrphanedMerits_ExistingBankedMeritForMissingTroop_MovesToDescendantBeforeNewKillsScored()
    {
        // A prior battle already banked 5 merit under troop_a, which has since fully upgraded
        // to troop_b (troop_a no longer has any count in the roster, but the id still resolves —
        // ConsolidateOrphanedMerits only DROPS entries whose troop id no longer resolves at all).
        _sut.ImportMerits(new Dictionary<string, int> { ["troop_a"] = 5 });
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 0, 10));

        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_b"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 3; i++)
            _sut.RegisterKill("troop_b");

        SetupHumanTroop("troop_b");
        _roster.GetTroopCount("troop_a").Returns(0);
        _roster.GetUpgradeTargetIds("troop_a").Returns(new List<string> { "troop_b" });
        _roster.GetTroopCount("troop_b").Returns(5);

        var offers = _sut.EndBattle(true);

        // 5 (consolidated from troop_a) + 3 (this battle's kills) = 8, exactly the threshold —
        // one offer is queued, but bug fix (a) means queuing never deducts: the merit itself
        // stays banked at 8 until CompleteOffer is actually called.
        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
        Assert.AreEqual(8, _sut.GetMerit("troop_b"));
        Assert.AreEqual(1, offers.Count);
    }

    // --- CompleteOffer (bug fix (a): deduct on completion only) ---

    [TestMethod]
    public void CompleteOffer_DeductsExactlyOneThreshold()
    {
        _sut.ImportMerits(new Dictionary<string, int> { ["troop_a"] = 20 });

        _sut.CompleteOffer("troop_a");

        Assert.AreEqual(12, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void CompleteOffer_NeverCalled_DeclinedOfferLosesNothing()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");
        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);
        _sut.EndBattle(true);

        // Player declines — CompleteOffer is never called.
        Assert.AreEqual(8, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void CompleteOffer_DeductionNeverGoesNegative()
    {
        _sut.ImportMerits(new Dictionary<string, int> { ["troop_a"] = 3 });

        _sut.CompleteOffer("troop_a");

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    // --- GrantMerit (backs taom.fc_grant_merit) ---

    [TestMethod]
    public void GrantMerit_PositiveAmount_AddsToBank()
    {
        _sut.GrantMerit("troop_a", 5);
        _sut.GrantMerit("troop_a", 3);

        Assert.AreEqual(8, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void GrantMerit_ZeroOrNegativeAmount_Ignored()
    {
        _sut.GrantMerit("troop_a", 0);
        _sut.GrantMerit("troop_a", -5);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    // --- CanPromote (race gate, fail-closed) ---

    [TestMethod]
    public void CanPromote_UnknownRaceId_FailsClosedAndNeverCallsNameLookup()
    {
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 99, 10));
        _raceManager.IsValidRaceId(99).Returns(false);

        var result = _sut.CanPromote("troop_a");

        Assert.IsFalse(result);
        _raceManager.DidNotReceive().GetRaceNameFromId(99);
    }

    [TestMethod]
    public void CanPromote_ValidRaceInAllowList_ReturnsTrue()
    {
        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(1);

        Assert.IsTrue(_sut.CanPromote("troop_a"));
    }

    [TestMethod]
    public void CanPromote_ValidRaceNotInAllowList_ReturnsFalse()
    {
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 5, 10));
        _raceManager.IsValidRaceId(5).Returns(true);
        _raceManager.GetRaceNameFromId(5).Returns("cave_troll");

        Assert.IsFalse(_sut.CanPromote("troop_a"));
    }

    [TestMethod]
    public void CanPromote_HeroTroop_ReturnsFalse()
    {
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", true, false, 0, 10));

        Assert.IsFalse(_sut.CanPromote("troop_a"));
    }

    [TestMethod]
    public void CanPromote_PrisonGuard_ReturnsFalse()
    {
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, true, 0, 10));

        Assert.IsFalse(_sut.CanPromote("troop_a"));
    }

    [TestMethod]
    public void CanPromote_MissingTroop_ReturnsFalse()
    {
        _roster.GetTroopInfo("troop_a").Returns(TroopInfo.Missing);

        Assert.IsFalse(_sut.CanPromote("troop_a"));
    }

    // --- Promoted-hero bookkeeping ---

    [TestMethod]
    public void RecordPromotedHero_AddsIdOnce()
    {
        _sut.RecordPromotedHero("hero_1");
        _sut.RecordPromotedHero("hero_1");

        Assert.AreEqual(1, _sut.GetPromotedHeroIds().Count(id => id == "hero_1"));
    }

    [TestMethod]
    public void PruneDeadPromotedHeroes_DropsIdsFailingIsAlive()
    {
        _sut.RecordPromotedHero("hero_alive");
        _sut.RecordPromotedHero("hero_dead");

        var survivors = _sut.PruneDeadPromotedHeroes(id => id == "hero_alive");

        CollectionAssert.AreEqual(new[] { "hero_alive" }, survivors.ToList());
        CollectionAssert.AreEqual(new[] { "hero_alive" }, _sut.GetPromotedHeroIds().ToList());
    }

    // --- SyncData round trip ---

    [TestMethod]
    public void ExportImportMerits_RoundTrips()
    {
        _sut.ImportMerits(new Dictionary<string, int> { ["troop_a"] = 4, ["troop_b"] = 9 });

        var exported = _sut.ExportMerits();

        Assert.AreEqual(4, exported["troop_a"]);
        Assert.AreEqual(9, exported["troop_b"]);
    }

    [TestMethod]
    public void ExportImportPromotedHeroIds_RoundTrips()
    {
        _sut.ImportPromotedHeroIds(new List<string> { "hero_1", "hero_2" });

        CollectionAssert.AreEqual(new[] { "hero_1", "hero_2" }, _sut.ExportPromotedHeroIds());
    }

    [TestMethod]
    public void ImportMerits_NullArgument_ClearsToEmpty()
    {
        _sut.ImportMerits(new Dictionary<string, int> { ["troop_a"] = 4 });

        _sut.ImportMerits(null);

        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
    }

    // --- Offer queue ---

    [TestMethod]
    public void TryDequeueOffer_EmptyQueue_ReturnsFalse()
    {
        var result = _sut.TryDequeueOffer(out var offer);

        Assert.IsFalse(result);
        Assert.IsNull(offer);
        Assert.IsFalse(_sut.HasPendingOffers);
    }

    [TestMethod]
    public void TryDequeueOffer_AfterQueuedOffer_ReturnsItAndDrainsQueue()
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");
        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(5);
        _sut.EndBattle(true);

        var result = _sut.TryDequeueOffer(out var offer);

        Assert.IsTrue(result);
        Assert.AreEqual("troop_a", offer.TroopId);
        Assert.IsFalse(_sut.HasPendingOffers);
    }
}
