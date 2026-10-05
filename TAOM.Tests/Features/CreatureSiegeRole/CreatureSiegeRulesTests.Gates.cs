using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureSiegeRole.Domain;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

// The gates: which castle gate counts, whether a creature may count on it being open, whether a creature is already
// through it, and whether a ram is at work in front of it.
public partial class CreatureSiegeRulesTests
{
    // --- which gate -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void ResolveGate_NoCandidates_IsNull()
    {
        Assert.IsNull(CreatureSiegeRules.ResolveGate(null));
        Assert.IsNull(CreatureSiegeRules.ResolveGate(List<SiegeGateReading>()));
    }

    [TestMethod]
    public void ResolveGate_TheCanonicalGateIsUsable_IsTheCanonicalGate()
    {
        var canonical = Gate(name: "canonical");

        var resolved = CreatureSiegeRules.ResolveGate(List(canonical, Gate(name: "other")));

        Assert.AreSame(canonical, resolved);
    }

    [TestMethod]
    public void ResolveGate_TheCanonicalGateIsHidden_FallsBackToTheFirstUsableOfTheSameTag()
    {
        // TAOM scenes carry hidden inner gates (visible="false"): vanilla takes the first tagged gate, a hidden stub.
        var hidden = Gate(name: "hidden stub", visible: false);
        var real = Gate(name: "real");

        Assert.AreSame(real, CreatureSiegeRules.ResolveGate(List(hidden, real, Gate(name: "later"))));
    }

    [TestMethod]
    public void ResolveGate_TheCanonicalGateIsDisabled_FallsBack()
    {
        var real = Gate(name: "real");

        Assert.AreSame(real, CreatureSiegeRules.ResolveGate(List(Gate(disabled: true), real)));
    }

    [TestMethod]
    public void ResolveGate_ACandidateThatIsHiddenAndDisabled_IsSkippedForTheNextUsableOne()
    {
        var real = Gate(name: "real");

        Assert.AreSame(real,
            CreatureSiegeRules.ResolveGate(List(Gate(visible: false), Gate(disabled: true), Gate(visible: false, disabled: true), real)));
    }

    [TestMethod]
    public void ResolveGate_NoCandidateIsUsable_IsNull()
    {
        Assert.IsNull(CreatureSiegeRules.ResolveGate(List(Gate(visible: false), Gate(disabled: true))));
    }

    // --- is the gate passable ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Track_AShutIntactGate_IsShut_AndNotPassable()
    {
        var track = CreatureSiegeRules.Track(GateTrack.Closed, destroyedNow: false, openNow: false, now: 10f);

        Assert.AreEqual(GateTrack.Closed, track);
        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, 10f));
        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, 1000f));
    }

    [TestMethod]
    public void Track_AGateSeenOpen_StartsItsClockAtTheFirstSighting()
    {
        var track = CreatureSiegeRules.Track(GateTrack.Closed, false, openNow: true, now: 10f);

        Assert.IsFalse(track.Destroyed);
        Assert.AreEqual(10f, track.OpenSince);
    }

    [TestMethod]
    public void IsPassable_AnOpenGate_NeedsTwoSecondsOfUnbrokenOpenness()
    {
        var track = CreatureSiegeRules.Track(GateTrack.Closed, false, true, 10f);

        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, 10f));
        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, 11.9f));
        Assert.IsTrue(CreatureSiegeRules.IsPassable(track, 12f), "exactly two seconds counts");
        Assert.IsTrue(CreatureSiegeRules.IsPassable(track, 60f));
    }

    [TestMethod]
    public void Track_AStillOpenGate_KeepsTheOriginalClock()
    {
        var first = CreatureSiegeRules.Track(GateTrack.Closed, false, true, 10f);
        var later = CreatureSiegeRules.Track(first, false, true, 11.5f);

        Assert.AreEqual(10f, later.OpenSince);
    }

    [TestMethod]
    public void Track_AGateThatShutsAgain_IsImpassableAtOnce_EvenAfterALongOpenStretch()
    {
        var open = CreatureSiegeRules.Track(GateTrack.Closed, false, true, 10f);
        Assert.IsTrue(CreatureSiegeRules.IsPassable(open, 30f));

        var reclosed = CreatureSiegeRules.Track(open, false, openNow: false, now: 30f);

        Assert.IsFalse(CreatureSiegeRules.IsPassable(reclosed, 30f));
        Assert.IsFalse(CreatureSiegeRules.IsPassable(reclosed, 31f));
    }

    [TestMethod]
    public void Track_ALeverThatFlapsEveryPass_NeverMakesTheGatePassable()
    {
        // A held lever calls OpenDoor and CloseDoor on every server tick (CastleGate.cs:726): the 0.5 s poll sees the door
        // open on one pass and shut on the next. It must never count as open for two seconds.
        var track = GateTrack.Closed;
        for (var i = 0; i < 200; i++)
        {
            var now = 10f + 0.5f * i;
            track = CreatureSiegeRules.Track(track, false, openNow: i % 2 == 0, now);

            Assert.IsFalse(CreatureSiegeRules.IsPassable(track, now), $"pass {i} at {now}");
        }
    }

    [TestMethod]
    public void Track_ADestroyedGate_IsLatchedPassableForGood()
    {
        var destroyed = CreatureSiegeRules.Track(GateTrack.Closed, destroyedNow: true, openNow: false, now: 10f);

        Assert.IsTrue(destroyed.Destroyed);
        Assert.IsTrue(CreatureSiegeRules.IsPassable(destroyed, 10f));

        // A later reading that says "intact and shut" does not undo it (a reset, a bad read).
        var later = CreatureSiegeRules.Track(destroyed, destroyedNow: false, openNow: false, now: 20f);
        Assert.IsTrue(later.Destroyed);
        Assert.IsTrue(CreatureSiegeRules.IsPassable(later, 20f));
        Assert.IsTrue(CreatureSiegeRules.IsPassable(later, float.NaN), "the latch needs no clock");
    }

    [TestMethod]
    public void Track_ANaNClock_NeverMakesAnOpenGatePassable()
    {
        var track = CreatureSiegeRules.Track(GateTrack.Closed, false, openNow: true, now: float.NaN);

        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, float.NaN));
        Assert.IsFalse(CreatureSiegeRules.IsPassable(track, 100f), "an open stretch that never had a real start is not one");
    }

    [TestMethod]
    public void Track_ANaNClockThenARealOne_StartsTheClockAtTheRealOne()
    {
        var blind = CreatureSiegeRules.Track(GateTrack.Closed, false, true, float.NaN);
        var seen = CreatureSiegeRules.Track(blind, false, true, 7f);

        Assert.AreEqual(7f, seen.OpenSince);
        Assert.IsFalse(CreatureSiegeRules.IsPassable(seen, 8f));
        Assert.IsTrue(CreatureSiegeRules.IsPassable(seen, 9f));
    }

    // --- is the creature already through the outer gate -------------------------------------------------------------

    // Gate at (100, 200) facing +Y: the attacker side is y > 200, the inner side y < 200.
    private static bool Past(int face = 0, float agentX = 100f, float agentY = 200f, float forwardX = 0f, float forwardY = 1f) =>
        CreatureSiegeRules.IsPastOuterGate(face, agentX, agentY, 100f, 200f, forwardX, forwardY);

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(11)]
    [DataRow(21)]
    [DataRow(101)]
    [DataRow(911)]
    public void IsPastOuterGate_AFaceIdEndingInOne_IsInsideTheCastle(int face)
    {
        // The engine's own inside test (TeamAISiegeComponent: InsideCastleNavMeshID is 1, and ids % 10 == 1 count).
        Assert.IsTrue(Past(face, agentY: 500f), "no geometry needed: the navmesh face says it");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(10)]
    [DataRow(333)]
    [DataRow(-1)]
    [DataRow(-9)]
    [DataRow(1000052)]
    public void IsPastOuterGate_AnyOtherFaceId_NeedsTheGeometry(int face)
    {
        Assert.IsFalse(Past(face, agentY: 250f), "outside, on the attacker side");
    }

    [TestMethod]
    public void IsPastOuterGate_OnTheInnerSideWithinFifteenMetres_IsPast()
    {
        Assert.IsTrue(Past(agentY: 195f));
        Assert.IsTrue(Past(agentX: 103f, agentY: 190f));
        Assert.IsTrue(Past(agentX: 96f, agentY: 190f), "exactly four to either side counts");
        Assert.IsTrue(Past(agentX: 104f, agentY: 195f));
    }

    [TestMethod]
    public void IsPastOuterGate_BehindTheFrameLineButFarToTheSide_IsNotPast()
    {
        // 10 m to the side, 5 m behind the gate's line: inside 15 m, but beside the castle wall, not through the gate.
        Assert.IsFalse(Past(agentX: 110f, agentY: 195f));
        Assert.IsFalse(Past(agentX: 90f, agentY: 195f));
        Assert.IsFalse(Past(agentX: 104.1f, agentY: 195f), "just past the four metre bound");
    }

    [TestMethod]
    public void IsPastOuterGate_TheLateralBoundFollowsTheGateAxis()
    {
        // The gate turned to face +X: lateral is now along Y.
        Assert.IsFalse(Past(agentX: 95f, agentY: 210f, forwardX: 1f, forwardY: 0f));
        Assert.IsTrue(Past(agentX: 95f, agentY: 203f, forwardX: 1f, forwardY: 0f));
    }

    [TestMethod]
    public void IsPastOuterGate_ANavmeshFaceEndingInOne_IgnoresTheLateralBound()
    {
        Assert.IsTrue(Past(face: 11, agentX: 130f, agentY: 195f));
    }

    [TestMethod]
    public void IsPastOuterGate_OnTheInnerSideButBeyondFifteenMetres_IsNotPast()
    {
        // A creature far to one side of the gate, or deep behind it, is not "through the gate".
        Assert.IsFalse(Past(agentY: 180f), "20 m in");
        Assert.IsFalse(Past(agentX: 130f, agentY: 199f), "30 m to the side");
    }

    [TestMethod]
    public void IsPastOuterGate_OnTheAttackerSide_IsNotPast()
    {
        Assert.IsFalse(Past(agentY: 205f));
        Assert.IsFalse(Past(agentY: 200f), "on the gate's own line is not through it");
    }

    [TestMethod]
    public void IsPastOuterGate_FollowsTheGateAxis_NotTheWorldAxes()
    {
        // The same gate turned to face +X: now the inner side is x < 100.
        Assert.IsTrue(Past(agentX: 95f, agentY: 200f, forwardX: 1f, forwardY: 0f));
        Assert.IsFalse(Past(agentX: 105f, agentY: 200f, forwardX: 1f, forwardY: 0f));
    }

    [TestMethod]
    public void IsPastOuterGate_AUnitAxisIsNotRequired_ButAUsableOneIs()
    {
        Assert.IsTrue(Past(agentY: 195f, forwardX: 0f, forwardY: 40f), "a scaled gate frame is normalised");
        Assert.IsFalse(Past(agentY: 195f, forwardX: 0f, forwardY: 0f), "a zero axis says nothing: not past");
    }

    [DataTestMethod]
    [DataRow(float.NaN, 195f, 0f, 1f)]
    [DataRow(100f, float.NaN, 0f, 1f)]
    [DataRow(100f, 195f, float.NaN, 1f)]
    [DataRow(100f, 195f, 0f, float.NaN)]
    [DataRow(float.PositiveInfinity, 195f, 0f, 1f)]
    [DataRow(100f, 195f, 0f, float.PositiveInfinity)]
    public void IsPastOuterGate_AnyNonFiniteInput_IsNotPast(float agentX, float agentY, float forwardX, float forwardY)
    {
        Assert.IsFalse(Past(agentX: agentX, agentY: agentY, forwardX: forwardX, forwardY: forwardY));
    }

    // --- the ram ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void RamWorking_NoRam_IsFalse()
    {
        Assert.IsFalse(CreatureSiegeRules.RamWorking(present: false, deactivated: false, userCount: 4, secondsSinceUsed: 0f));
    }

    [TestMethod]
    public void RamWorking_ADeactivatedRam_IsFalse_HoweverManyStandAtIt()
    {
        // The ram deactivates once its gate is destroyed, or is open and the ram has arrived.
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, deactivated: true, userCount: 4, secondsSinceUsed: 0f));
    }

    [TestMethod]
    public void RamWorking_AgentsStandAtIt_IsTrue()
    {
        Assert.IsTrue(CreatureSiegeRules.RamWorking(true, false, userCount: 1, secondsSinceUsed: float.NaN));
    }

    [TestMethod]
    public void RamWorking_NoOneAtItButAFormationUsedItWithinTwentySeconds_IsTrue()
    {
        Assert.IsTrue(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: 0f));
        Assert.IsTrue(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: 19.9f));
        Assert.IsTrue(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: 20f), "exactly twenty counts");
    }

    [TestMethod]
    public void RamWorking_NoOneAtItAndNotUsedForAWhile_IsFalse()
    {
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: 20.1f));
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: 600f));
    }

    [TestMethod]
    public void RamWorking_NeverUsed_IsFalse_AndANaNClockIsNotAVerdict()
    {
        // The service passes NaN for a ram it has never seen used (and for a broken clock): not working.
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: float.NaN));
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, false, 0, secondsSinceUsed: float.PositiveInfinity));
    }

    [TestMethod]
    public void RamWorking_ANegativeUserCount_DoesNotCount()
    {
        Assert.IsFalse(CreatureSiegeRules.RamWorking(true, false, userCount: -1, secondsSinceUsed: float.NaN));
    }
}
