using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Refuge;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Decision-path coverage for <see cref="WardenService"/>: candidate ordering and the
/// companion-slot gate, the promote-exactly-one-troop sequencing, the release matrix under
/// the NO-KILL contract (companion and promoted warden both return by action, absent no-op), and
/// the never-attached promotion rollback. Campaign statics are
/// overridden on a test subclass; those virtual bodies are the honest untested boundary sliver.
/// </summary>
[TestClass]
public class WardenServiceTests
{
    private sealed class TestableWardenService : WardenService
    {
        public readonly List<WardenCandidate> Companions = new List<WardenCandidate>();
        public readonly List<WardenCandidate> Promotables = new List<WardenCandidate>();
        public bool SlotFree = true;
        public readonly Dictionary<string, int> TroopCounts = new Dictionary<string, int>();
        public string MintResult = "hero_minted";
        public int MintCalls;
        public readonly List<string> RemovedTroops = new List<string>();
        public bool WithRefuge = true;
        public readonly List<string> MovedToMainParty = new List<string>();

        public TestableWardenService(IModLogger logger)
            : base(logger)
        {
        }

        protected override IReadOnlyList<WardenCandidate> CompanionsInMainParty() => Companions;

        protected override bool HasCompanionSlotFree() => SlotFree;

        protected override IReadOnlyList<WardenCandidate> PromotableTroopsInMainParty() => Promotables;

        protected override int TroopCountInMainParty(string troopId) =>
            TroopCounts.TryGetValue(troopId, out var count) ? count : 0;

        protected override string MintCompanionFromTroop(string troopId)
        {
            MintCalls++;
            return MintResult;
        }

        protected override bool RemoveOneTroopFromMainParty(string troopId)
        {
            RemovedTroops.Add(troopId);
            return true;
        }

        protected override bool IsHeroWithRefugeParty(string heroId) => WithRefuge;

        protected override void MoveHeroToMainParty(string heroId) => MovedToMainParty.Add(heroId);

        public bool RemoveMintedResult = true;
        public readonly List<string> RemovedCompanions = new List<string>();
        public readonly List<string> RefundedTroops = new List<string>();

        protected override bool RemoveMintedCompanion(string heroId)
        {
            RemovedCompanions.Add(heroId);
            return RemoveMintedResult;
        }

        protected override void AddOneTroopToMainParty(string troopId) => RefundedTroops.Add(troopId);
    }

    private TestableWardenService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TestableWardenService(Substitute.For<IModLogger>());
    }

    private static WardenCandidate Companion(string id) =>
        new WardenCandidate { Id = id, DisplayName = id, IsCompanion = true };

    private static WardenCandidate Troop(string id) =>
        new WardenCandidate { Id = id, DisplayName = id, IsCompanion = false };

    // --- Candidates ordering + gating ---

    [TestMethod]
    public void Candidates_CompanionsListedBeforePromotableTroops()
    {
        _sut.Companions.Add(Companion("companion_1"));
        _sut.Promotables.Add(Troop("troop_1"));
        _sut.Companions.Add(Companion("companion_2"));

        var candidates = _sut.Candidates();

        Assert.AreEqual(3, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
        Assert.AreEqual("companion_2", candidates[1].Id);
        Assert.AreEqual("troop_1", candidates[2].Id);
        Assert.IsTrue(candidates[0].IsCompanion);
        Assert.IsFalse(candidates[2].IsCompanion);
    }

    [TestMethod]
    public void Candidates_NoCompanionSlotFree_ExcludesTroopsKeepsCompanions()
    {
        _sut.SlotFree = false;
        _sut.Companions.Add(Companion("companion_1"));
        _sut.Promotables.Add(Troop("troop_1"));

        var candidates = _sut.Candidates();

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
    }

    [TestMethod]
    public void Candidates_NobodyAvailable_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.Candidates().Count);
        Assert.IsFalse(_sut.AnyAvailable());
    }

    [TestMethod]
    public void AnyAvailable_TroopOnlyWithSlotFree_True()
    {
        _sut.Promotables.Add(Troop("troop_1"));

        Assert.IsTrue(_sut.AnyAvailable());
    }

    [TestMethod]
    public void AnyAvailable_TroopOnlyWithoutSlot_False()
    {
        _sut.SlotFree = false;
        _sut.Promotables.Add(Troop("troop_1"));

        Assert.IsFalse(_sut.AnyAvailable());
    }

    // --- ResolveWarden ---

    [TestMethod]
    public void ResolveWarden_Companion_ReturnsHisIdWithoutPromotion()
    {
        var heroId = _sut.ResolveWarden(Companion("companion_1"), out bool promoted, out string fromTroop);

        Assert.AreEqual("companion_1", heroId);
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
        Assert.AreEqual(0, _sut.MintCalls);
        Assert.AreEqual(0, _sut.RemovedTroops.Count);
    }

    [TestMethod]
    public void ResolveWarden_Troop_MintsHeroAndConsumesExactlyOneSoldier()
    {
        _sut.TroopCounts["troop_1"] = 5;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out string fromTroop);

        Assert.AreEqual("hero_minted", heroId);
        Assert.IsTrue(promoted);
        Assert.AreEqual("troop_1", fromTroop);
        Assert.AreEqual(1, _sut.MintCalls);
        CollectionAssert.AreEqual(new[] { "troop_1" }, _sut.RemovedTroops,
            "exactly one soldier leaves the ranks; he became the hero");
    }

    [TestMethod]
    public void ResolveWarden_TroopWithoutCompanionSlot_FailsWithoutMinting()
    {
        _sut.SlotFree = false;
        _sut.TroopCounts["troop_1"] = 5;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out _);

        Assert.IsNull(heroId);
        Assert.IsFalse(promoted);
        Assert.AreEqual(0, _sut.MintCalls);
        Assert.AreEqual(0, _sut.RemovedTroops.Count);
    }

    [TestMethod]
    public void ResolveWarden_TroopStackEmptiedSincePicking_FailsWithoutMinting()
    {
        _sut.TroopCounts["troop_1"] = 0;

        Assert.IsNull(_sut.ResolveWarden(Troop("troop_1"), out _, out _));
        Assert.AreEqual(0, _sut.MintCalls);
    }

    [TestMethod]
    public void ResolveWarden_MintRefused_FailsWithoutConsumingTheSoldier()
    {
        _sut.TroopCounts["troop_1"] = 5;
        _sut.MintResult = null;

        var heroId = _sut.ResolveWarden(Troop("troop_1"), out bool promoted, out string fromTroop);

        Assert.IsNull(heroId);
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
        Assert.AreEqual(0, _sut.RemovedTroops.Count, "no hero means the soldier stays a soldier");
    }

    [TestMethod]
    public void ResolveWarden_NullCandidate_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveWarden(null, out bool promoted, out string fromTroop));
        Assert.IsFalse(promoted);
        Assert.IsNull(fromTroop);
    }

    [TestMethod]
    public void ResolveWarden_EmptyCandidateId_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveWarden(new WardenCandidate { Id = "", IsCompanion = true }, out _, out _));
    }

    // --- ReleaseWarden (the NO-KILL matrix) ---

    [TestMethod]
    public void ReleaseWarden_CompanionWithRefuge_RejoinsMainParty()
    {
        _sut.WithRefuge = true;

        _sut.ReleaseWarden("companion_1", promoted: false);

        CollectionAssert.AreEqual(new[] { "companion_1" }, _sut.MovedToMainParty);
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenWithRefuge_MovesByActionAndIsNeverKilled()
    {
        _sut.WithRefuge = true;

        _sut.ReleaseWarden("hero_minted", promoted: true);

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.MovedToMainParty,
            "a promoted warden rides the same AddHeroToPartyAction as a companion; a raw roster "
            + "merge nulls a hero's PartyBelongedTo when the source roster clears");
        Assert.AreEqual(0, _sut.RemovedCompanions.Count, "release NEVER kills or de-promotes");
        Assert.AreEqual(0, _sut.RefundedTroops.Count, "the soldier became somebody; no refund");
    }

    [TestMethod]
    public void ReleaseWarden_PromotedWardenElsewhere_NoOp()
    {
        _sut.WithRefuge = false;

        _sut.ReleaseWarden("hero_minted", promoted: true);

        Assert.AreEqual(0, _sut.MovedToMainParty.Count,
            "a captured promoted warden is left where fate put him, same as a companion");
    }

    // --- UnwindPromotion (the never-attached rollback window) ---

    [TestMethod]
    public void UnwindPromotion_RemovesMintedCompanionAndRefundsTheSoldier()
    {
        _sut.UnwindPromotion("hero_minted", "troop_a");

        CollectionAssert.AreEqual(new[] { "hero_minted" }, _sut.RemovedCompanions);
        CollectionAssert.AreEqual(new[] { "troop_a" }, _sut.RefundedTroops,
            "exactly the one soldier the promotion consumed comes back");
    }

    [TestMethod]
    public void UnwindPromotion_RemoveRefused_NoRefund()
    {
        _sut.RemoveMintedResult = false;

        _sut.UnwindPromotion("hero_minted", "troop_a");

        Assert.AreEqual(0, _sut.RefundedTroops.Count,
            "a refund without the removal would duplicate the soldier");
    }

    [TestMethod]
    public void UnwindPromotion_NullOrEmptyArgs_NoOp()
    {
        _sut.UnwindPromotion(null, "troop_a");
        _sut.UnwindPromotion("hero_minted", null);
        _sut.UnwindPromotion("", "");

        Assert.AreEqual(0, _sut.RemovedCompanions.Count);
        Assert.AreEqual(0, _sut.RefundedTroops.Count);
    }

    [TestMethod]
    public void ReleaseWarden_CompanionElsewhere_NoOp()
    {
        _sut.WithRefuge = false;

        _sut.ReleaseWarden("companion_1", promoted: false);

        Assert.AreEqual(0, _sut.MovedToMainParty.Count,
            "a captured or hospitalised warden is left where fate put him");
    }

    [TestMethod]
    public void ReleaseWarden_NullOrEmptyHeroId_NoOp()
    {
        _sut.ReleaseWarden(null, promoted: false);
        _sut.ReleaseWarden("", promoted: false);

        Assert.AreEqual(0, _sut.MovedToMainParty.Count);
    }
}
