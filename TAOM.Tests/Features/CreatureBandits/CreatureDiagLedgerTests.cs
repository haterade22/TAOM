using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits.Diagnostics;

// Creature Bandits diagnostics (#692): the per-mission ledger. Serials number creatures per mission (never
// Agent.Index, which the engine recycles); the line budget keeps a flood out of the crash tail (report.txt carries
// the last 500 lines); everything resets at the mission boundary, so a second battle in one launch logs again.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureDiagLedgerTests
{
    [TestMethod]
    public void Register_NumbersCreaturesFromOne()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 5, missionCap: 100);
        Assert.AreEqual(1, ledger.Register("a", 1f, 10).Serial);
        Assert.AreEqual(2, ledger.Register("b", 2f, 11).Serial);
        Assert.AreEqual(2, ledger.Count, "the count is the last serial handed out");
        Assert.AreEqual("b", ledger.Get(2)!.TroopId);
        Assert.IsNull(ledger.Get(3));
    }

    [TestMethod]
    public void TryTakeLine_PerCreatureKindCap_SuppressesAndCounts()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 2, missionCap: 100);
        var record = ledger.Register("a", 0f, 1);

        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "hit"));
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "hit"));
        Assert.AreEqual(LineVerdict.Suppress, ledger.TryTakeLine(1, "hit"));
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "bite"), "the cap is per kind");
        Assert.AreEqual(1, record.SuppressedLines);
    }

    [TestMethod]
    public void TryTakeLine_MissionCap_ReportsTheCapOnceThenSuppresses()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 100, missionCap: 2);
        ledger.Register("a", 0f, 1);

        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "x"));
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "y"));
        Assert.AreEqual(LineVerdict.CapReached, ledger.TryTakeLine(1, "z"));
        Assert.AreEqual(LineVerdict.Suppress, ledger.TryTakeLine(1, "w"));
        Assert.AreEqual(2, ledger.MissionSuppressed);
    }

    [TestMethod]
    public void TryTakeLine_OutcomeKinds_StillWritePastTheMissionCap()
    {
        // A long fight must not starve the deaths and warnings: they are bounded by the creature count.
        var ledger = new CreatureDiagLedger(perCreatureCap: 100, missionCap: 1);
        ledger.Register("a", 0f, 1);
        ledger.TryTakeLine(1, "hit-taken");

        Assert.AreEqual(LineVerdict.CapReached, ledger.TryTakeLine(1, "bite"));
        foreach (var kind in new[] { "removed", "backstop", "outcome", "sides" })
            Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, kind), kind);
    }

    [TestMethod]
    public void TryTakeLine_OutcomeKinds_KeepTheirPerCreatureCap()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 1, missionCap: 100);
        ledger.Register("a", 0f, 1);
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "outcome"));
        Assert.AreEqual(LineVerdict.Suppress, ledger.TryTakeLine(1, "outcome"));
    }

    [TestMethod]
    public void TryTakeLine_MissionLevelLines_UseSerialZeroAndTheMissionCapOnly()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 1, missionCap: 10);
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(0, "snap"));
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(0, "snap"), "mission lines have no per-kind cap");
    }

    [TestMethod]
    public void Reset_ClearsRecordsBudgetsAndSerials()
    {
        var ledger = new CreatureDiagLedger(perCreatureCap: 1, missionCap: 1);
        ledger.Register("a", 0f, 1);
        ledger.TryTakeLine(1, "x");

        ledger.Reset();

        Assert.AreEqual(0, ledger.Count);
        Assert.AreEqual(1, ledger.Register("b", 0f, 2).Serial);
        Assert.AreEqual(LineVerdict.Write, ledger.TryTakeLine(1, "x"));
    }

    [TestMethod]
    public void AnyRegistered_FalseUntilACreatureRegisters_AndAgainAfterReset()
    {
        // The diagnostics behavior's agent callbacks exit on this before any serial lookup, so a mission with no
        // creature pays one field read per hit, removal or alarm.
        var ledger = new CreatureDiagLedger(perCreatureCap: 5, missionCap: 100);
        Assert.IsFalse(ledger.AnyRegistered);

        ledger.Register("a", 0f, 1);
        Assert.IsTrue(ledger.AnyRegistered);

        ledger.Reset();
        Assert.IsFalse(ledger.AnyRegistered);
    }

    [TestMethod]
    public void HpBand_SplitsAtQuarters_AndMarksBadInputs()
    {
        Assert.AreEqual(4, CreatureDiagLedger.HpBand(100f, 100f));
        Assert.AreEqual(3, CreatureDiagLedger.HpBand(75f, 100f));
        Assert.AreEqual(2, CreatureDiagLedger.HpBand(50f, 100f));
        Assert.AreEqual(1, CreatureDiagLedger.HpBand(10f, 100f));
        Assert.AreEqual(0, CreatureDiagLedger.HpBand(0f, 100f));
        Assert.AreEqual(-1, CreatureDiagLedger.HpBand(float.NaN, 100f));
        Assert.AreEqual(-1, CreatureDiagLedger.HpBand(50f, 0f));
    }

    [TestMethod]
    public void IsStuck_ChasingButNotMoving_IsStuck()
        => Assert.IsTrue(CreatureDiagLedger.IsStuck(hasTarget: true, targetDistance: 8f, reach: 3.5f,
            attacking: false, movedMeters: 0.2f));

    [TestMethod]
    public void IsStuck_InReachAttackingMovingOrIdle_IsNotStuck()
    {
        Assert.IsFalse(CreatureDiagLedger.IsStuck(true, 2f, 3.5f, false, 0.1f), "in reach");
        Assert.IsFalse(CreatureDiagLedger.IsStuck(true, 8f, 3.5f, true, 0.1f), "attacking");
        Assert.IsFalse(CreatureDiagLedger.IsStuck(true, 8f, 3.5f, false, 2f), "moving");
        Assert.IsFalse(CreatureDiagLedger.IsStuck(false, 8f, 3.5f, false, 0f), "no target");
    }

    [TestMethod]
    public void IsStuck_NaNDistanceOrMovement_IsNotReportedAsStuck()
    {
        // Positive-polarity gates: NaN never produces a verdict.
        Assert.IsFalse(CreatureDiagLedger.IsStuck(true, float.NaN, 3.5f, false, 0f));
        Assert.IsFalse(CreatureDiagLedger.IsStuck(true, 8f, 3.5f, false, float.NaN));
    }

    [TestMethod]
    public void IsStalledInContact_PressedAgainstTargetWithoutAttacking_IsStalled()
        // The case the stuck check cannot see: inside the strike radius, outside the 1.5 m gate, not biting.
        => Assert.IsTrue(CreatureDiagLedger.IsStalledInContact(speed: 0.1f, targetDistance: 1.6f, strikeRadius: 3.5f,
            attacking: false));

    [TestMethod]
    public void IsStalledInContact_MovingAttackingFarOrUnknown_IsNotStalled()
    {
        Assert.IsFalse(CreatureDiagLedger.IsStalledInContact(2f, 1.6f, 3.5f, false), "moving");
        Assert.IsFalse(CreatureDiagLedger.IsStalledInContact(0.1f, 1.6f, 3.5f, true), "attacking");
        Assert.IsFalse(CreatureDiagLedger.IsStalledInContact(0.1f, 8f, 3.5f, false), "out of contact");
        Assert.IsFalse(CreatureDiagLedger.IsStalledInContact(0.1f, float.NaN, 3.5f, false), "no target");
        Assert.IsFalse(CreatureDiagLedger.IsStalledInContact(float.NaN, 1.6f, 3.5f, false), "no speed");
    }

    [TestMethod]
    public void IsWhiff_OnlyAnEnemyInTheArcWithNothingStruck()
    {
        Assert.IsTrue(CreatureDiagLedger.IsWhiff(enemiesInArc: 1, struck: 0));
        Assert.IsFalse(CreatureDiagLedger.IsWhiff(enemiesInArc: 0, struck: 0), "an empty arc or brood-mates only is not a miss");
        Assert.IsFalse(CreatureDiagLedger.IsWhiff(enemiesInArc: 2, struck: 1));
    }

    [TestMethod]
    public void ClassifyEngage_NamesWhyTheGateFailed()
    {
        Assert.AreEqual(CreatureEngageOutcome.ScanEmpty, CreatureDiagLedger.ClassifyEngage(candidates: 0, inRange: 0, passed: 0));
        Assert.AreEqual(CreatureEngageOutcome.RejectedRange, CreatureDiagLedger.ClassifyEngage(3, 0, 0));
        Assert.AreEqual(CreatureEngageOutcome.RejectedCone, CreatureDiagLedger.ClassifyEngage(3, 2, 0));
        Assert.AreEqual(CreatureEngageOutcome.Passed, CreatureDiagLedger.ClassifyEngage(3, 2, 1));
    }

    [TestMethod]
    public void IsSideStalled_EmptyButUndepletedPastTheThreshold()
    {
        Assert.IsTrue(CreatureDiagLedger.IsSideStalled(liveAgents: 0, depleted: false, zeroSince: 10f, now: 21f, thresholdSeconds: 10f));
        Assert.IsFalse(CreatureDiagLedger.IsSideStalled(0, false, 10f, 19f, 10f), "not long enough");
        Assert.IsFalse(CreatureDiagLedger.IsSideStalled(0, true, 10f, 60f, 10f), "depleted: the battle can end");
        Assert.IsFalse(CreatureDiagLedger.IsSideStalled(3, false, 10f, 60f, 10f), "agents alive");
        Assert.IsFalse(CreatureDiagLedger.IsSideStalled(0, false, float.NaN, 60f, 10f), "never empty");
    }
}
