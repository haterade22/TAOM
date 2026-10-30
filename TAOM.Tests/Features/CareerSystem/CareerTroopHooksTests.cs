using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Hooks;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E10: the engine-facing seam for the three troop counters. An <c>Agent</c> cannot be built outside the
/// game, so the public overloads reduce it to a leader id and a class and an internal overload decides; these
/// tests drive the internal ones and the null-agent path. The hooks run on the damage and morale paths of every
/// mission, so they must hand their input back whenever anything is missing (no service, no agent, no leader),
/// and say so once in the log when something throws. The source-text pins that wire the hooks into the models
/// and fear sites are in <see cref="CareerTroopHooksWiringTests"/>, which needs no game.
/// </summary>
[TestClass]
public class CareerTroopHooksTests
{
    // ── fail open ──

    [TestMethod]
    public void NullAgent_EveryHookHandsItsInputBack()
    {
        var stats = Substitute.For<ICareerAgentStatService>();

        Assert.AreEqual(12f, CareerTroopHooks.ScaleFearDrain(stats, null, 12f));
        Assert.AreEqual(0.7f, CareerTroopHooks.KnockDownResistance(stats, null, 0.7f));
        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(stats, null, true));
        Assert.IsFalse(CareerTroopHooks.ApplyCrushHold(stats, null, false));
        stats.DidNotReceiveWithAnyArgs().ReportHookFault(default!, default!);
    }

    [TestMethod]
    public void NoStatsService_NullAgentHooksStillHandTheirInputBack()
    {
        Assert.AreEqual(12f, CareerTroopHooks.ScaleFearDrain(null, null, 12f));
        Assert.AreEqual(0.7f, CareerTroopHooks.KnockDownResistance(null, null, 0.7f));
        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(null, null, true));
    }

    [TestMethod]
    public void TryResolveTroop_NullAgent_IsFalse()
    {
        Assert.IsFalse(CareerTroopHooks.TryResolveTroop(null, out var leader, out var troopClass));
        Assert.IsNull(leader);
        Assert.AreEqual(TroopClassMask.None, troopClass);
    }

    [TestMethod]
    public void NoStatsService_EveryDecisionPassesItsInputThrough()
    {
        Assert.AreEqual(12f, CareerTroopHooks.ScaleFearDrain(null, "lord", TroopClassMask.Infantry, 12f));
        Assert.AreEqual(0.7f, CareerTroopHooks.KnockDownResistance(null, "lord", TroopClassMask.Infantry, 0.7f));
        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(null, "lord", TroopClassMask.Infantry, true, () => 0f));
        Assert.IsFalse(CareerTroopHooks.ApplyCrushHold(null, "lord", TroopClassMask.Infantry, false, () => 0f));
    }

    [TestMethod]
    public void NoLeader_NothingIsAskedOfTheService()
    {
        var stats = Substitute.For<ICareerAgentStatService>();

        Assert.AreEqual(12f, CareerTroopHooks.ScaleFearDrain(stats, null, TroopClassMask.None, 12f));
        Assert.AreEqual(0.7f, CareerTroopHooks.KnockDownResistance(stats, null, TroopClassMask.None, 0.7f));
        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(stats, null, TroopClassMask.None, true, () => 0f));

        stats.DidNotReceiveWithAnyArgs().ScaleTroopFear(default, default, default);
        stats.DidNotReceiveWithAnyArgs().ScaleTroopKnockdownResistance(default, default, default);
        stats.DidNotReceiveWithAnyArgs().HoldsAgainstCrush(default, default, default!);
    }

    // ── a throwing service: the input comes back and the fault is reported ──

    [TestMethod]
    public void ScaleFearDrain_TheServiceThrows_HandsTheDrainBackAndReportsTheHook()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        var boom = new InvalidOperationException("boom");
        stats.ScaleTroopFear("lord", TroopClassMask.Infantry, 12f).Throws(boom);

        Assert.AreEqual(12f, CareerTroopHooks.ScaleFearDrain(stats, "lord", TroopClassMask.Infantry, 12f));
        stats.Received(1).ReportHookFault(nameof(CareerTroopHooks.ScaleFearDrain), boom);
    }

    [TestMethod]
    public void KnockDownResistance_TheServiceThrows_HandsTheResistanceBackAndReportsTheHook()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        var boom = new InvalidOperationException("boom");
        stats.ScaleTroopKnockdownResistance("lord", TroopClassMask.Cavalry, 0.7f).Throws(boom);

        Assert.AreEqual(0.7f, CareerTroopHooks.KnockDownResistance(stats, "lord", TroopClassMask.Cavalry, 0.7f));
        stats.Received(1).ReportHookFault(nameof(CareerTroopHooks.KnockDownResistance), boom);
    }

    [TestMethod]
    public void ApplyCrushHold_TheServiceThrows_KeepsTheCrushAndReportsTheHook()
    {
        // Fail open means the engine's verdict stands: a crush that could not be checked stays a crush.
        var stats = Substitute.For<ICareerAgentStatService>();
        var boom = new InvalidOperationException("boom");
        stats.HoldsAgainstCrush("lord", TroopClassMask.Infantry, Arg.Any<Func<float>>()).Throws(boom);

        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(stats, "lord", TroopClassMask.Infantry, true, () => 0f));
        stats.Received(1).ReportHookFault(nameof(CareerTroopHooks.ApplyCrushHold), boom);
    }

    // ── deciding ──

    [TestMethod]
    public void ScaleFearDrain_ReturnsTheServicesAnswerForTheLeaderAndClass()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.ScaleTroopFear("lord", TroopClassMask.Cavalry, 12f).Returns(3f);

        Assert.AreEqual(3f, CareerTroopHooks.ScaleFearDrain(stats, "lord", TroopClassMask.Cavalry, 12f));
    }

    [TestMethod]
    public void KnockDownResistance_ReturnsTheServicesAnswerForTheLeaderAndClass()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.ScaleTroopKnockdownResistance("lord", TroopClassMask.Infantry, 0.7f).Returns(1.4f);

        Assert.AreEqual(1.4f, CareerTroopHooks.KnockDownResistance(stats, "lord", TroopClassMask.Infantry, 0.7f));
    }

    [TestMethod]
    public void ApplyCrushHold_CrushedAndTheDefenderHolds_BecomesAHold()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.HoldsAgainstCrush("lord", TroopClassMask.Infantry, Arg.Any<Func<float>>()).Returns(true);

        Assert.IsFalse(CareerTroopHooks.ApplyCrushHold(stats, "lord", TroopClassMask.Infantry, true, () => 0f));
    }

    [TestMethod]
    public void ApplyCrushHold_CrushedAndTheDefenderDoesNotHold_StaysCrushed()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.HoldsAgainstCrush("lord", TroopClassMask.Infantry, Arg.Any<Func<float>>()).Returns(false);

        Assert.IsTrue(CareerTroopHooks.ApplyCrushHold(stats, "lord", TroopClassMask.Infantry, true, () => 0f));
    }

    [TestMethod]
    public void ApplyCrushHold_NotCrushed_IsNeverTurnedIntoACrushAndTheRollIsNotSpent()
    {
        var stats = Substitute.For<ICareerAgentStatService>();
        stats.HoldsAgainstCrush(default, default, default!).ReturnsForAnyArgs(true);

        Assert.IsFalse(CareerTroopHooks.ApplyCrushHold(stats, "lord", TroopClassMask.Infantry, false, () => 0f));
        stats.DidNotReceiveWithAnyArgs().HoldsAgainstCrush(default, default, default!);
    }
}
