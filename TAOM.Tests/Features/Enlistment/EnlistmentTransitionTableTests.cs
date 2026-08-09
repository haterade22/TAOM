using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentTransitionTableTests
{
    // The complete legal-edge set from the approved design. The matrix test below asserts
    // BOTH directions: every listed edge is legal, every unlisted pair is illegal.
    private static readonly HashSet<(EnlistmentState From, EnlistmentState To)> ExpectedLegalEdges = new()
    {
        (EnlistmentState.NotEnlisted, EnlistmentState.PetitionPending),
        (EnlistmentState.PetitionPending, EnlistmentState.NotEnlisted),
        (EnlistmentState.PetitionPending, EnlistmentState.EnlistedAttached),
        (EnlistmentState.EnlistedAttached, EnlistmentState.EnlistedBattle),
        (EnlistmentState.EnlistedBattle, EnlistmentState.EnlistedAttached),
        // (EnlistedAttached -> EnlistedDetachedOnDuty) DELETED 2026-08-09. Field duties stopped
        // detaching, so nothing produces the state; the OUTBOUND edges below are kept as a
        // recovery path in case the parse-time coercion ever regresses.
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.EnlistedAttached),
        (EnlistmentState.EnlistedAttached, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedBattle, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.EnlistedPlayerCaptive),
        (EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.EnlistedAttached),
        (EnlistmentState.EnlistedAttached, EnlistmentState.CommanderUnavailable),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.CommanderUnavailable),
        (EnlistmentState.CommanderUnavailable, EnlistmentState.EnlistedAttached),
        (EnlistmentState.EnlistedAttached, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedBattle, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedDetachedOnDuty, EnlistmentState.Discharging),
        (EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.Discharging),
        (EnlistmentState.CommanderUnavailable, EnlistmentState.Discharging),
        (EnlistmentState.Discharging, EnlistmentState.NotEnlisted),
    };

    [TestMethod]
    public void IsLegal_FullMatrix_MatchesDesignExactly()
    {
        var states = Enum.GetValues(typeof(EnlistmentState)).Cast<EnlistmentState>().ToArray();
        foreach (var from in states)
        {
            foreach (var to in states)
            {
                var expected = ExpectedLegalEdges.Contains((from, to));
                Assert.AreEqual(
                    expected,
                    EnlistmentTransitionTable.IsLegal(from, to),
                    $"Transition {from} -> {to} expected {(expected ? "LEGAL" : "ILLEGAL")}");
            }
        }
    }

    [TestMethod]
    public void IsLegal_DischargingIsTheOnlyPathToNotEnlistedFromService()
    {
        // The commission-softlock fix by construction: no enlisted-family state may jump
        // straight to NotEnlisted without passing through the discharge pipeline.
        foreach (var from in new[]
        {
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedBattle,
            EnlistmentState.EnlistedDetachedOnDuty,
            EnlistmentState.EnlistedPlayerCaptive,
            EnlistmentState.CommanderUnavailable,
        })
        {
            Assert.IsFalse(
                EnlistmentTransitionTable.IsLegal(from, EnlistmentState.NotEnlisted),
                $"{from} must not skip the Discharging pipeline");
        }

        Assert.IsTrue(EnlistmentTransitionTable.IsLegal(EnlistmentState.Discharging, EnlistmentState.NotEnlisted));
    }

    [TestMethod]
    public void IsLegal_EveryEnlistedFamilyStateCanReachDischarging()
    {
        foreach (var from in new[]
        {
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedBattle,
            EnlistmentState.EnlistedDetachedOnDuty,
            EnlistmentState.EnlistedPlayerCaptive,
            EnlistmentState.CommanderUnavailable,
        })
        {
            Assert.IsTrue(
                EnlistmentTransitionTable.IsLegal(from, EnlistmentState.Discharging),
                $"{from} must be dischargeable");
        }
    }

    [TestMethod]
    public void IsLegal_SelfTransitions_AllIllegal()
    {
        foreach (var state in Enum.GetValues(typeof(EnlistmentState)).Cast<EnlistmentState>())
        {
            Assert.IsFalse(EnlistmentTransitionTable.IsLegal(state, state), $"{state} -> {state} must be illegal");
        }
    }

    [TestMethod]
    public void IsLegal_CaptiveNeverEntersBattleOrDuty()
    {
        // Vanilla captivity owns the party; the machine must never route a captive
        // player into battle interception or detached duty.
        Assert.IsFalse(EnlistmentTransitionTable.IsLegal(
            EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.EnlistedBattle));
        Assert.IsFalse(EnlistmentTransitionTable.IsLegal(
            EnlistmentState.EnlistedPlayerCaptive, EnlistmentState.EnlistedDetachedOnDuty));
    }
}
