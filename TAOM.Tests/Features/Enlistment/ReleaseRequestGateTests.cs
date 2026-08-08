using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The release gate decides whether asking to leave is granted, refused for now, or refused
/// until a term is served. Every "refused" path must be escapable and every degenerate input
/// must fall OPEN (granted) — a gate that traps the player in service forever is strictly worse
/// than one that lets them go early, which is the mistake this feature already made once
/// (every exit classified Desertion against a 365-day contract, fixed 2026-08-07).
/// </summary>
[TestClass]
public class ReleaseRequestGateTests
{
    private IEnlistmentStore _store;
    private ICommanderLordAdapter _commander;
    private IPlayerContextAdapter _playerContext;
    private IEnlistmentConfigProvider _config;
    private EnlistmentDialogGateService _sut;

    [TestInitialize]
    public void Setup()
    {
        _store = Substitute.For<IEnlistmentStore>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();
        _config = Substitute.For<IEnlistmentConfigProvider>();
        _config.GetConfig().Returns(new EnlistmentCoreConfig { MinimumServiceDays = 21.0 });
        _store.Record.Returns(new EnlistmentRecord());
        _sut = new EnlistmentDialogGateService(_store, _commander, _playerContext, _config, null);
    }

    private void Enlist(double atDay, EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.CommanderHeroId = "lord_1";
        _store.Record.EnlistedAtDay = atDay;
    }

    [TestMethod]
    public void EvaluateReleaseRequest_NotEnlisted_Granted()
    {
        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(100.0).Verdict);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_TermServed_Granted()
    {
        Enlist(atDay: 10.0);
        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(31.0).Verdict);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_ExactlyOnTheTermDay_Granted()
    {
        // Boundary: "21 days of service" means day 21 counts as served, not "one more day".
        Enlist(atDay: 10.0);
        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(31.0).Verdict);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_MidTerm_RefusedTooSoonWithRealDayCount()
    {
        // The proof case: asked on day 3 of a 21-day term.
        Enlist(atDay: 0.0);
        var request = _sut.EvaluateReleaseRequest(3.0);

        Assert.AreEqual(ReleaseVerdict.RefusedTooSoon, request.Verdict);
        Assert.AreEqual(18, request.DaysOwed);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_LastFractionalDay_ReportsAtLeastOneDay()
    {
        // Rounding must never produce "0 more days are owed" — that reads as a bug.
        Enlist(atDay: 0.0);
        var request = _sut.EvaluateReleaseRequest(20.9999);

        Assert.AreEqual(ReleaseVerdict.RefusedTooSoon, request.Verdict);
        Assert.AreEqual(1, request.DaysOwed);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_InBattle_RefusedInBattle()
    {
        // Outranks the term check: even a veteran cannot walk off mid-engagement.
        Enlist(atDay: 0.0, state: EnlistmentState.EnlistedBattle);
        Assert.AreEqual(ReleaseVerdict.RefusedInBattle, _sut.EvaluateReleaseRequest(999.0).Verdict);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_NoEnlistmentDayRecorded_Granted()
    {
        // A record without a start day cannot prove a debt. Fail open — never trap the player
        // on the strength of missing data.
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.CommanderHeroId = "lord_1";
        _store.Record.EnlistedAtDay = null;

        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(3.0).Verdict);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(0.0)]
    [DataRow(-5.0)]
    public void EvaluateReleaseRequest_DegenerateMinimumTerm_Granted(double minimum)
    {
        // NaN comparisons are always false, so the gate is written as a positive requirement
        // to REFUSE. A poisoned config must not become a life sentence.
        _config.GetConfig().Returns(new EnlistmentCoreConfig { MinimumServiceDays = minimum });
        Enlist(atDay: 0.0);

        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(1.0).Verdict);
    }

    [TestMethod]
    public void EvaluateReleaseRequest_NaNCurrentDay_Granted()
    {
        Enlist(atDay: 0.0);
        Assert.AreEqual(ReleaseVerdict.Granted, _sut.EvaluateReleaseRequest(double.NaN).Verdict);
    }

    [TestMethod]
    public void ClassifyLeaveReason_StillAlwaysPlayerRequest()
    {
        // Batch 1's fix must survive Batch 6: a GRANTED release is never desertion, whatever
        // the term says. Desertion is now produced by exactly one place — the confirmed
        // desert branch — and never by classification.
        Enlist(atDay: 0.0);
        Assert.AreEqual(DischargeReason.PlayerRequest, _sut.ClassifyLeaveReason(1.0));
    }
}
