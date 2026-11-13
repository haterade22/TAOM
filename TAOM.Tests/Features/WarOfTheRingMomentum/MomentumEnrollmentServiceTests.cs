using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumEnrollmentServiceTests
{
    private IWarOfTheRingService _wotrService = null!;
    private IAllianceAdapter _allianceAdapter = null!;
    private IAlignmentService _alignmentService = null!;
    private IModLogger _logger = null!;
    private MomentumWarState _state = null!;
    private MomentumEnrollmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _wotrService = Substitute.For<IWarOfTheRingService>();
        _allianceAdapter = Substitute.For<IAllianceAdapter>();
        _alignmentService = Substitute.For<IAlignmentService>();
        _logger = Substitute.For<IModLogger>();
        _state = new MomentumWarState();

        _wotrService.CurrentPhase.Returns(WarPhase.FullWar);
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "vlandia", "empire_s", "umbar" });
        // An unmocked GetKingdomCultureId answers an empty culture, so these kingdoms resolve on their id alone.
        _alignmentService.ResolveSide("empire_w", "").Returns(FactionSide.Free);
        _alignmentService.ResolveSide("vlandia", "").Returns(FactionSide.Free);
        _alignmentService.ResolveSide("empire_s", "").Returns(FactionSide.Evil);
        _alignmentService.ResolveSide("umbar", "").Returns(FactionSide.Neutral);

        _sut = new MomentumEnrollmentService(_wotrService, _allianceAdapter, _alignmentService, _logger);
    }

    [TestMethod]
    public void SweepEnrollment_FullWar_EnrollsFreeAndEvilNeverNeutral()
    {
        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsTrue(changed);
        Assert.IsTrue(_state.Free.ContainsKingdom("empire_w"));
        Assert.IsTrue(_state.Free.ContainsKingdom("vlandia"));
        Assert.IsTrue(_state.Evil.ContainsKingdom("empire_s"));
        Assert.IsFalse(_state.DoesKingdomTakePart("umbar"));
    }

    [TestMethod]
    public void SweepEnrollment_SidesEachKingdomOnItsOwnIdAndCulturePair()
    {
        // The pair is the kingdom's id with the culture of that same kingdom, resolved by the alignment service itself.
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "ghost_realm" });
        _allianceAdapter.GetKingdomCultureId("ghost_realm").Returns("mordor");
        _alignmentService.ResolveSide("ghost_realm", "mordor").Returns(FactionSide.Evil);

        _sut.SweepEnrollment(_state);

        Assert.IsTrue(_state.Evil.ContainsKingdom("ghost_realm"));
        Assert.IsFalse(_state.Free.ContainsKingdom("ghost_realm"));
    }

    [TestMethod]
    public void SweepEnrollment_FirstSweep_StartsWar()
    {
        _sut.SweepEnrollment(_state);
        Assert.IsTrue(_state.HasWarStarted);
    }

    [TestMethod]
    public void SweepEnrollment_BeforeFullWar_NoOp()
    {
        _wotrService.CurrentPhase.Returns(WarPhase.IsengardWar);

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsFalse(changed);
        Assert.IsFalse(_state.HasWarStarted);
        Assert.AreEqual(0, _state.Free.KingdomIds.Count);
    }

    [TestMethod]
    public void SweepEnrollment_WarEnded_NoOp()
    {
        _state.MarkWarEnded(WarOutcome.FreeVictory);

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsFalse(changed);
        Assert.AreEqual(0, _state.Free.KingdomIds.Count);
    }

    [TestMethod]
    public void SweepEnrollment_SecondSweep_NoDuplicatesReturnsFalse()
    {
        _sut.SweepEnrollment(_state);
        bool changedAgain = _sut.SweepEnrollment(_state);

        Assert.IsFalse(changedAgain);
        Assert.AreEqual(2, _state.Free.KingdomIds.Count);
        Assert.AreEqual(1, _state.Evil.KingdomIds.Count);
    }

    [TestMethod]
    public void SweepEnrollment_LateCreatedKingdom_EnrolledOnNextSweep()
    {
        _sut.SweepEnrollment(_state);

        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "vlandia", "empire_s", "umbar", "player_kingdom" });
        _alignmentService.ResolveSide("player_kingdom", "").Returns(FactionSide.Free);

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsTrue(changed);
        Assert.IsTrue(_state.Free.ContainsKingdom("player_kingdom"));
    }

    [TestMethod]
    public void SweepEnrollment_OnlyNeutralKingdoms_DoesNotStartWar()
    {
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "umbar" });

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsFalse(changed);
        Assert.IsFalse(_state.HasWarStarted);
    }

    [TestMethod]
    public void RemoveKingdom_EnrolledEitherSide_RemovesAndReturnsTrue()
    {
        _sut.SweepEnrollment(_state);
        // As at KingdomDestroyed: the engine has marked the kingdom eliminated, so it is off the live list.
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "vlandia", "umbar" });

        Assert.IsTrue(_sut.RemoveKingdom(_state, "empire_s"));
        Assert.IsFalse(_state.DoesKingdomTakePart("empire_s"));

        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "umbar" });
        Assert.IsTrue(_sut.RemoveKingdom(_state, "vlandia"));
        Assert.IsFalse(_state.DoesKingdomTakePart("vlandia"));
    }

    // #1113: the enrolled sets are only as fresh as the last sweep, and a pledge changes a kingdom's side
    // inside a day. The caller counts each side's kingdoms right after this call (the elimination victory),
    // so a realm that took a side since the last sweep must already be in the count. The engine marks the
    // kingdom eliminated before it raises KingdomDestroyed (v1.5.7 DestroyKingdomAction.cs:9), so the
    // destroyed kingdom is off the live list here.
    [TestMethod]
    public void RemoveKingdom_ARealmTookASideSinceTheLastSweep_EnrollsItBeforeTheSideIsCounted()
    {
        _sut.SweepEnrollment(_state);
        _alignmentService.ResolveSide("umbar", "").Returns(FactionSide.Evil);
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "vlandia", "umbar" });

        Assert.IsTrue(_sut.RemoveKingdom(_state, "empire_s"));

        Assert.IsFalse(_state.Evil.ContainsKingdom("empire_s"), "the destroyed kingdom is not enrolled again");
        Assert.IsTrue(_state.Evil.ContainsKingdom("umbar"));
        Assert.AreEqual(1, _state.Evil.KingdomIds.Count, "the side still has a kingdom: no elimination victory");
    }

    [TestMethod]
    public void RemoveKingdom_Unknown_ReturnsFalse()
    {
        Assert.IsFalse(_sut.RemoveKingdom(_state, "nope"));
    }

    // ---- Codex #327: player-founded kingdom culture fallback ----

    [TestMethod]
    public void SweepEnrollment_PlayerFoundedKingdom_EnrollsByCulture()
    {
        // A dynamically-created kingdom id isn't in alignment.json, so its own pair falls back to its
        // culture, which IS classified: it must still enroll on that culture's side.
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string>
        {
            "empire_w", "vlandia", "empire_s", "umbar", "new_kingdom"
        });
        _allianceAdapter.GetKingdomCultureId("new_kingdom").Returns("gondor");
        _alignmentService.ResolveSide("new_kingdom", "gondor").Returns(FactionSide.Free);

        _sut.SweepEnrollment(_state);

        Assert.IsTrue(_state.Free.ContainsKingdom("new_kingdom"));
    }

    [TestMethod]
    public void SweepEnrollment_NeutralKingdomWithNeutralCulture_StillExcluded()
    {
        // The culture fallback must not enroll a genuinely-neutral kingdom.
        _allianceAdapter.GetKingdomCultureId("umbar").Returns("umbar");
        _alignmentService.ResolveSide("umbar", "umbar").Returns(FactionSide.Neutral);

        _sut.SweepEnrollment(_state);

        Assert.IsFalse(_state.DoesKingdomTakePart("umbar"));
    }

    [TestMethod]
    public void SweepEnrollment_AsksForTheCultureOfEveryKingdom_SoThePairIsOneEntitys()
    {
        _sut.SweepEnrollment(_state);

        // The alignment service owns the fallback, so it gets the kingdom's own culture every time.
        _allianceAdapter.Received().GetKingdomCultureId("empire_w");
        _alignmentService.Received().ResolveSide("empire_w", "");
    }

    // ---- Codex #327: prune stale enrolled ids ----

    [TestMethod]
    public void SweepEnrollment_EnrolledKingdomNoLongerLive_PrunedFromSide()
    {
        // empire_s enrolled, then destroyed while the feature was disabled (RemoveKingdom
        // never fired). On the next sweep it's gone from GetAllKingdomIds → must be pruned
        // so the elimination-victory count can reach 0.
        _sut.SweepEnrollment(_state);
        Assert.IsTrue(_state.Evil.ContainsKingdom("empire_s"));

        _allianceAdapter.GetAllKingdomIds().Returns(new List<string>
        {
            "empire_w", "vlandia", "umbar" // empire_s eliminated
        });

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsTrue(changed);
        Assert.IsFalse(_state.Evil.ContainsKingdom("empire_s"));
        Assert.AreEqual(0, _state.Evil.KingdomIds.Count);
    }

    [TestMethod]
    public void SweepEnrollment_AllLiveKingdomsStillEnrolled_NoPrune()
    {
        _sut.SweepEnrollment(_state);
        var freeBefore = _state.Free.KingdomIds.Count;
        var evilBefore = _state.Evil.KingdomIds.Count;

        _sut.SweepEnrollment(_state);

        Assert.AreEqual(freeBefore, _state.Free.KingdomIds.Count);
        Assert.AreEqual(evilBefore, _state.Evil.KingdomIds.Count);
    }

    [TestMethod]
    public void SweepEnrollment_EnrolledKingdomBecameNeutral_RemovedFromSide()
    {
        // The Khand case: alignment.json flipped an enrolled kingdom to Neutral. The next
        // sweep must drop it from its side (its kingdom-side AND culture-side are Neutral).
        _sut.SweepEnrollment(_state);
        Assert.IsTrue(_state.Evil.ContainsKingdom("empire_s"));

        _alignmentService.ResolveSide("empire_s", "").Returns(FactionSide.Neutral);
        // GetKingdomCultureId("empire_s") is unmocked, so its pair is (empire_s, "") and resolves Neutral.

        bool changed = _sut.SweepEnrollment(_state);

        Assert.IsTrue(changed);
        Assert.IsFalse(_state.DoesKingdomTakePart("empire_s"));
    }

    [TestMethod]
    public void SweepEnrollment_EnrolledKingdomFlippedSides_MovesToNewSide()
    {
        // An enrolled Evil kingdom whose alignment flips to Free is removed from Evil and
        // re-added to Free in the same sweep.
        _sut.SweepEnrollment(_state);
        Assert.IsTrue(_state.Evil.ContainsKingdom("empire_s"));

        _alignmentService.ResolveSide("empire_s", "").Returns(FactionSide.Free);

        _sut.SweepEnrollment(_state);

        Assert.IsFalse(_state.Evil.ContainsKingdom("empire_s"));
        Assert.IsTrue(_state.Free.ContainsKingdom("empire_s"));
    }
}
