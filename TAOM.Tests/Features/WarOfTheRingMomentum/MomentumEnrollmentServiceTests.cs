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
}
