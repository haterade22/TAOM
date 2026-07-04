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
        _alignmentService.GetKingdomSide("empire_w").Returns(FactionSide.Free);
        _alignmentService.GetKingdomSide("vlandia").Returns(FactionSide.Free);
        _alignmentService.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
        _alignmentService.GetKingdomSide("umbar").Returns(FactionSide.Neutral);

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
        _alignmentService.GetKingdomSide("player_kingdom").Returns(FactionSide.Free);

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

        Assert.IsTrue(_sut.RemoveKingdom(_state, "empire_s"));
        Assert.IsFalse(_state.DoesKingdomTakePart("empire_s"));

        Assert.IsTrue(_sut.RemoveKingdom(_state, "vlandia"));
        Assert.IsFalse(_state.DoesKingdomTakePart("vlandia"));
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
        // A dynamically-created kingdom id isn't in alignment.json → GetKingdomSide Neutral;
        // but its culture IS classified, so it must still enroll on that culture's side.
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string>
        {
            "empire_w", "vlandia", "empire_s", "umbar", "new_kingdom"
        });
        _alignmentService.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
        _allianceAdapter.GetKingdomCultureId("new_kingdom").Returns("gondor");
        _alignmentService.GetCultureSide("gondor").Returns(FactionSide.Free);

        _sut.SweepEnrollment(_state);

        Assert.IsTrue(_state.Free.ContainsKingdom("new_kingdom"));
    }

    [TestMethod]
    public void SweepEnrollment_NeutralKingdomWithNeutralCulture_StillExcluded()
    {
        // The culture fallback must not enroll a genuinely-neutral kingdom.
        _allianceAdapter.GetKingdomCultureId("umbar").Returns("umbar");
        _alignmentService.GetCultureSide("umbar").Returns(FactionSide.Neutral);

        _sut.SweepEnrollment(_state);

        Assert.IsFalse(_state.DoesKingdomTakePart("umbar"));
    }

    [TestMethod]
    public void SweepEnrollment_KingdomWithSideAlreadyKnown_DoesNotConsultCulture()
    {
        _sut.SweepEnrollment(_state);

        // empire_w resolved Free by kingdom id — the culture fallback must not run for it.
        _allianceAdapter.DidNotReceive().GetKingdomCultureId("empire_w");
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
}
