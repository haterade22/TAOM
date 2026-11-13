using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HallSeats;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Tests.Features.HallSeats;

/// <summary>The spawn-pass lifecycle of Hall Seats: when it plans, when it holds a lord back, and that it fails open.</summary>
[TestClass]
public class HallSeatsServiceTests
{
    private IHallSeatsAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private HallSeatsService _sut = null!;
    private readonly object _handler = new object();

    [TestInitialize]
    public void SetUp()
    {
        _adapter = Substitute.For<IHallSeatsAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new HallSeatsService(_adapter, _logger);
    }

    private HallSeatCandidate[] HallWith(int lords, int freeSpots, int takenByOthers = 0)
    {
        var candidates = Enumerable.Range(0, lords)
            .Select(i => new HallSeatCandidate(new object(), "lord_" + i.ToString("D2"), false, false, false, 1, 0f))
            .ToArray();
        _adapter.ReadLordsHall(_handler).Returns(new HallSeatSnapshot("sturgia_castle_keep_a_l2_interior", candidates, freeSpots, takenByOthers));
        return candidates;
    }

    [TestMethod]
    public void OutsideAPass_NothingIsHeldBack_AndTheHallIsNotRead()
    {
        var lords = HallWith(10, 2);

        Assert.IsFalse(_sut.ShouldHoldBack(lords[9].Token));
        _adapter.DidNotReceiveWithAnyArgs().ReadLordsHall(default!);
    }

    [TestMethod]
    public void SwitchOff_OpensNoPass()
    {
        var lords = HallWith(10, 2);

        Assert.IsFalse(_sut.BeginPass(_handler, enabled: false));
        Assert.IsFalse(_sut.InPass);
        Assert.IsFalse(_sut.ShouldHoldBack(lords[9].Token));
    }

    [TestMethod]
    public void InAPass_HoldsBackTheLordsPastTheSeats()
    {
        var lords = HallWith(10, 2);

        Assert.IsTrue(_sut.BeginPass(_handler, enabled: true));
        var held = lords.Count(l => _sut.ShouldHoldBack(l.Token));

        Assert.AreEqual(8, held);
    }

    [TestMethod]
    public void ThePlanIsMadeOncePerPass_OnTheFirstSpawn()
    {
        var lords = HallWith(4, 1);
        _sut.BeginPass(_handler, true);

        foreach (var lord in lords)
            _sut.ShouldHoldBack(lord.Token);
        _sut.ShouldHoldBack(new object());

        _adapter.Received(1).ReadLordsHall(_handler);
    }

    [TestMethod]
    public void EndPass_ClearsThePlan_SoALordWalkingInLaterSpawns()
    {
        var lords = HallWith(4, 1);
        _sut.BeginPass(_handler, true);
        Assert.IsTrue(_sut.ShouldHoldBack(lords[3].Token));

        _sut.EndPass();

        Assert.IsFalse(_sut.InPass);
        Assert.IsFalse(_sut.ShouldHoldBack(lords[3].Token));
    }

    [TestMethod]
    public void ANewPass_ReadsTheHallAgain()
    {
        HallWith(4, 1);
        _sut.BeginPass(_handler, true);
        _sut.ShouldHoldBack(new object());
        _sut.EndPass();

        _sut.BeginPass(_handler, true);
        _sut.ShouldHoldBack(new object());

        _adapter.Received(2).ReadLordsHall(_handler);
    }

    [TestMethod]
    public void ASecondBegin_WhileAPassIsOpen_IsRefused()
    {
        HallWith(1, 1);
        Assert.IsTrue(_sut.BeginPass(_handler, true));

        Assert.IsFalse(_sut.BeginPass(new object(), true));
        Assert.IsTrue(_sut.InPass);
    }

    [TestMethod]
    public void NotALordsHall_NothingIsHeldBack()
    {
        _adapter.ReadLordsHall(_handler).Returns((HallSeatSnapshot?)null);
        _sut.BeginPass(_handler, true);

        Assert.IsFalse(_sut.ShouldHoldBack(new object()));
    }

    [TestMethod]
    public void AdapterThrows_FailsOpen_LogsOnce()
    {
        _adapter.ReadLordsHall(_handler).Returns(_ => throw new InvalidOperationException("boom"));
        _sut.BeginPass(_handler, true);

        Assert.IsFalse(_sut.ShouldHoldBack(new object()));
        Assert.IsFalse(_sut.ShouldHoldBack(new object()));

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[HallSeats]") && s.Contains("boom")));
    }

    [TestMethod]
    public void HoldingBack_LogsOneInfoLine_WithTheCounts()
    {
        var lords = HallWith(40, 5);
        _sut.BeginPass(_handler, true);

        foreach (var lord in lords)
            _sut.ShouldHoldBack(lord.Token);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("40 lords") && s.Contains("seated 5") && s.Contains("held back 35")));
    }

    [TestMethod]
    public void NothingHeldBack_LogsNothing()
    {
        var lords = HallWith(2, 5);
        _sut.BeginPass(_handler, true);

        foreach (var lord in lords)
            Assert.IsFalse(_sut.ShouldHoldBack(lord.Token));

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }
}
