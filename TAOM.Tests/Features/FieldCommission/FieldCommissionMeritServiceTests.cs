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
            // Deliberately far above the shipped default of 1. Most tests here are about merit
            // arithmetic and the per-troop roster cap; leaving the battle cap at 1 would mask those
            // by truncating every result to a single offer. The cap has its own tests below.
            MaxOffersPerBattle = 99,
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

    // --- Offers-per-battle cap ---

    [TestMethod]
    public void EndBattle_MoreOffersEarnedThanCapAllows_QueuesOnlyTheCap()
    {
        // The defect this pins: every queued offer is a separate game-pausing modal inquiry, and the
        // tick pump shows them back to back. Uncapped, one big won battle reads to the player as the
        // game locking up behind a wall of dialogs.
        _config.MaxOffersPerBattle = 2;
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 10 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 80; i++) // 80 merit / 8 = 10 possible offers
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(10);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(2, offers.Count);
    }

    [TestMethod]
    public void EndBattle_CapReached_MeritAboveTheCapStaysBanked()
    {
        // The cap throttles presentation, it must never destroy earned merit — otherwise capping
        // would quietly be a nerf rather than a pacing fix.
        _config.MaxOffersPerBattle = 1;
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 10 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 80; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(10);

        _sut.EndBattle(true);

        Assert.AreEqual(80, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void EndBattle_CapReachedOnFirstTroop_LaterTroopsStillBankTheirMerit()
    {
        // The donor mod's hard-won lesson, re-pinned: the scan must keep RUNNING once the offer
        // budget is spent. An early return here would silently discard the kills of every troop type
        // ordered after the first promotable one.
        _config.MaxOffersPerBattle = 1;
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 10, ["troop_b"] = 10 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 40; i++)
            _sut.RegisterKill("troop_a"); // highest kills — scanned first
        for (var i = 0; i < 16; i++)
            _sut.RegisterKill("troop_b");

        SetupHumanTroop("troop_a");
        SetupHumanTroop("troop_b");
        _roster.GetTroopCount("troop_a").Returns(10);
        _roster.GetTroopCount("troop_b").Returns(10);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(1, offers.Count, "budget is one offer for the whole battle");
        Assert.AreEqual("troop_a", offers[0].TroopId);
        Assert.AreEqual(16, _sut.GetMerit("troop_b"), "troop_b earned no offer but must keep its merit");
    }

    [TestMethod]
    public void EndBattle_CapBelowOne_TreatedAsOne()
    {
        // Defence in depth. The config provider and the MCM clamp both reject < 1, so this can only
        // arrive from a future caller — and a 0 here would read as "promotions on" while queueing
        // nothing, which is the one failure mode a master toggle exists to make impossible.
        _config.MaxOffersPerBattle = 0;
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 10 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");

        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(10);

        var offers = _sut.EndBattle(true);

        Assert.AreEqual(1, offers.Count);
    }

    // --- Declining suppresses the re-offer ---

    /// <summary>
    /// Wins one eligible battle in which <paramref name="troopId"/> scores <paramref name="kills"/>
    /// kills, with <paramref name="rosterCount"/> of that type still standing afterwards.
    /// </summary>
    private IReadOnlyList<PendingPromotionOffer> WinBattle(string troopId, int kills, int rosterCount = 10)
    {
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { [troopId] = rosterCount });
        _sut.BeginBattle(true);
        for (var i = 0; i < kills; i++)
            _sut.RegisterKill(troopId);

        SetupHumanTroop(troopId);
        _roster.GetTroopCount(troopId).Returns(rosterCount);
        return _sut.EndBattle(true);
    }

    [TestMethod]
    public void EndBattle_AfterDecline_DoesNotReOfferUntilAnotherThresholdIsEarned()
    {
        // Merit is never spent on a refusal, and merit only ever grows — so without a decline mark
        // the queue condition (bank >= threshold) stays true forever and the same soldier is
        // proposed again after every won battle. That is the nag the player cannot switch off.
        WinBattle("troop_a", 8);          // 8 merit -> 1 offer
        _sut.TryDequeueOffer(out var offer);
        _sut.RecordDeclinedOffer(offer.TroopId);

        var second = WinBattle("troop_a", 4);   // now 12 merit: still under 8 + 8

        Assert.AreEqual(0, second.Count);
        Assert.AreEqual(12, _sut.GetMerit("troop_a"), "merit keeps accruing — declining costs nothing");
    }

    [TestMethod]
    public void EndBattle_AfterDecline_OffersAgainOnceAnotherThresholdIsEarned()
    {
        // The other half of the contract: the refusal delays the ask, it must not end it.
        // Pinned at the shipped cap of 1 so this measures the decline mark, not the batch budget —
        // 16 merit at a threshold of 8 genuinely backs two promotions.
        _config.MaxOffersPerBattle = 1;
        WinBattle("troop_a", 8);
        _sut.TryDequeueOffer(out var offer);
        _sut.RecordDeclinedOffer(offer.TroopId);

        var second = WinBattle("troop_a", 8);   // 16 merit >= 8 + 8

        Assert.AreEqual(1, second.Count);
    }

    [TestMethod]
    public void CompleteOffer_ClearsAnEarlierDeclineMark()
    {
        // Accepting means the player changed their mind about this troop type; the next soldier who
        // distinguishes themselves should be judged on their own merit, not against a stale refusal.
        WinBattle("troop_a", 8);
        _sut.TryDequeueOffer(out var declined);
        _sut.RecordDeclinedOffer(declined.TroopId);
        _sut.CompleteOffer("troop_a"); // -8 -> 0 merit, mark cleared

        var next = WinBattle("troop_a", 8); // back to 8

        Assert.AreEqual(1, next.Count);
    }

    [TestMethod]
    public void RecordDeclinedOffer_NullOrEmptyTroopId_IsANoOp()
    {
        _sut.RecordDeclinedOffer(null);
        _sut.RecordDeclinedOffer(string.Empty);

        Assert.AreEqual(0, _sut.ExportDeclinedMarks().Count);
    }

    [TestMethod]
    public void ImportDeclinedMarks_RoundTripsThroughExport()
    {
        _sut.ImportDeclinedMarks(new Dictionary<string, int> { ["troop_a"] = 16 });

        CollectionAssert.AreEqual(
            new Dictionary<string, int> { ["troop_a"] = 16 },
            _sut.ExportDeclinedMarks());
    }

    [TestMethod]
    public void ImportDeclinedMarks_Null_ClearsMarks()
    {
        // A save written before the decline mark existed leaves SyncData's ref null. That must read
        // as "nothing declined", not throw and not keep the previous campaign's marks.
        _sut.ImportDeclinedMarks(new Dictionary<string, int> { ["troop_a"] = 16 });

        _sut.ImportDeclinedMarks(null);

        Assert.AreEqual(0, _sut.ExportDeclinedMarks().Count);
    }

    [TestMethod]
    public void EndBattle_MeritTransferredToHeir_DropsTheSourceTypesDeclineMark()
    {
        // A decline mark is an ABSOLUTE merit level. When the bank behind it is emptied or moved to
        // an upgraded heir, a surviving mark measures the next offer against a total the type no
        // longer has — and marks are only cleared by a completed promotion, so that type could never
        // be offered again. Bought by re-recruiting the old tier after upgrading a stack.
        WinBattle("troop_a", 24, rosterCount: 5);
        _sut.TryDequeueOffer(out var offer);
        _sut.RecordDeclinedOffer(offer.TroopId);        // mark at 24
        Assert.AreEqual(24, _sut.ExportDeclinedMarks()["troop_a"]);

        // troop_a upgrades out of the party entirely; its merit moves to troop_b.
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 5 });
        _sut.BeginBattle(true);
        _sut.RegisterKill("troop_a");
        SetupHumanTroop("troop_a");
        SetupHumanTroop("troop_b");
        _roster.GetTroopCount("troop_a").Returns(0);
        _roster.GetTroopCount("troop_b").Returns(5);
        _roster.GetUpgradeTargetIds("troop_a").Returns(new[] { "troop_b" });
        _sut.EndBattle(true);

        CollectionAssert.DoesNotContain(_sut.ExportDeclinedMarks().Keys.ToList(), "troop_a");
        Assert.AreEqual(0, _sut.GetMerit("troop_a"));
        Assert.AreEqual(25, _sut.GetMerit("troop_b"), "the merit itself must still reach the heir");
    }

    // --- Outstanding offers are debited against the bank ---

    [TestMethod]
    public void EndBattle_OffersStillQueued_DoesNotIssueMoreThanTheBankCanBack()
    {
        // Two won eligible battles inside one uninterrupted encounter (siege sally-outs do this) both
        // score before the tick pump gets to show anything. Reading the raw bank twice would issue two
        // offers backed by one threshold's worth of merit, and CompleteOffer's Math.Max(0, ...) would
        // hide the shortfall by charging the second one nothing.
        _config.MaxOffersPerBattle = 99;
        WinBattle("troop_a", 8);              // 8 merit -> 1 offer queued, NOT dequeued
        Assert.IsTrue(_sut.HasPendingOffers);

        var second = WinBattle("troop_a", 1); // 9 merit, but 8 of it is already spoken for

        Assert.AreEqual(0, second.Count);
    }

    [TestMethod]
    public void EndBattle_OffersQueuedButBankCoversBoth_IssuesTheSecond()
    {
        // The mirror: the debit must not become a blanket "never queue while anything is pending".
        _config.MaxOffersPerBattle = 99;
        WinBattle("troop_a", 8);
        Assert.IsTrue(_sut.HasPendingOffers);

        var second = WinBattle("troop_a", 8); // 16 merit backs two offers

        Assert.AreEqual(1, second.Count);
    }

    // --- Diagnostics gate ---

    [TestMethod]
    public void Trace_DiagnosticsOff_WritesNothing()
    {
        _config.Diagnostics = false;

        WinBattle("troop_a", 8);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Trace_DiagnosticsOn_LogsBattleAndMeritAndOffer()
    {
        // The switch exists so a player's next log answers "did this battle count, did the troop earn
        // merit, was an offer raised" without another round trip. Assert all three actually appear.
        _config.Diagnostics = true;

        WinBattle("troop_a", 8);

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("battle started")));
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("merit banked")));
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("offer queued")));
    }

    // --- Pending-offer queue lifetime ---

    [TestMethod]
    public void ClearPendingOffers_QueuedOffers_QueueIsEmptiedAndMeritKept()
    {
        // The queue is un-persisted state on a process-lifetime singleton. Loading a second save
        // without restarting must not inherit the first save's offers — but the merit bank is
        // persisted per-save and must not be collateral damage.
        _roster.GetRosterSnapshot().Returns(new Dictionary<string, int> { ["troop_a"] = 10 });
        _sut.BeginBattle(true);
        for (var i = 0; i < 8; i++)
            _sut.RegisterKill("troop_a");
        SetupHumanTroop("troop_a");
        _roster.GetTroopCount("troop_a").Returns(10);
        _sut.EndBattle(true);
        Assert.IsTrue(_sut.HasPendingOffers, "precondition: an offer must actually be queued");

        _sut.ClearPendingOffers();

        Assert.IsFalse(_sut.HasPendingOffers);
        Assert.IsFalse(_sut.TryDequeueOffer(out _));
        Assert.AreEqual(8, _sut.GetMerit("troop_a"));
    }

    [TestMethod]
    public void ClearPendingOffers_NothingQueued_IsANoOp()
    {
        _sut.ClearPendingOffers();

        Assert.IsFalse(_sut.HasPendingOffers);
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

    [TestMethod]
    public void ForgetPromotedHero_KnownId_RemovesItFromExport()
    {
        // A dismissed companion (#540) must leave the list at once, not at the next load's prune:
        // the persisted list and taom.fc_status both read it in between.
        _sut.ImportPromotedHeroIds(new List<string> { "hero_1", "hero_2" });

        _sut.ForgetPromotedHero("hero_1");

        CollectionAssert.AreEqual(new[] { "hero_2" }, _sut.ExportPromotedHeroIds());
    }

    [TestMethod]
    public void ForgetPromotedHero_UnknownId_IsANoOp()
    {
        _sut.ImportPromotedHeroIds(new List<string> { "hero_1" });

        _sut.ForgetPromotedHero("hero_never_promoted");

        CollectionAssert.AreEqual(new[] { "hero_1" }, _sut.ExportPromotedHeroIds());
    }

    [TestMethod]
    public void ForgetPromotedHero_NullOrEmptyId_IsANoOp()
    {
        _sut.ImportPromotedHeroIds(new List<string> { "hero_1" });

        _sut.ForgetPromotedHero(null);
        _sut.ForgetPromotedHero(string.Empty);

        CollectionAssert.AreEqual(new[] { "hero_1" }, _sut.ExportPromotedHeroIds());
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
