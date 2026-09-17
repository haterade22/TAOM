using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The first A/B battle (2026-09-17, Erebor v Mordor): the wall spent its opening minute
/// walking to a hill while the enemy horse were already on it, and the foot turned to chase
/// every eored that swung past. Two rules answer both: a foot formation's target is the nearest
/// enemy FOOT formation and horse matter only while inbound inside a distance; and the high
/// ground is worth a march only when it is close and nobody is on us yet.
/// </summary>
[TestClass]
public class EngagementRulesTests
{
    private static readonly EngagementTunables Tunables = new EngagementTunables(cavalryMattersMetres: 100f, highGroundMaxMetres: 60f, holdWhenEnemyWithinMetres: 50f);

    [TestMethod]
    public void Defaults_MatchTheShippedNumbers()
    {
        // 100 m is about the form-up allowance at horse speed (9 to 13 m/s): the wall needs
        // 8 to 12 s to go from a line to a square, so 40 m (3 to 4 s) was too late.
        var d = EngagementTunables.Default;
        Assert.AreEqual(100f, d.CavalryMattersMetres);
        Assert.AreEqual(60f, d.HighGroundMaxMetres);
        Assert.AreEqual(50f, d.HoldWhenEnemyWithinMetres);
    }

    [TestMethod]
    public void Pick_NearestInfantryWins_OverANearerHorse()
    {
        var pick = new TargetSelection();
        pick.Consider(index: 0, TargetClass.Horse, distance: 20f, threat: false);
        pick.Consider(index: 1, TargetClass.Infantry, distance: 90f, threat: false);
        pick.Consider(index: 2, TargetClass.Infantry, distance: 70f, threat: false);
        Assert.AreEqual(2, pick.Target);
    }

    [TestMethod]
    public void Pick_InfantryWithinOneAndAHalfTimesTheArchersDistance_IsPreferredToTheArchers()
    {
        // A retreating archer block is a slower version of the eored chase; the infantry beside
        // it is the fight.
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Archers, 40f, threat: false);
        pick.Consider(1, TargetClass.Infantry, 60f, threat: false);
        Assert.AreEqual(1, pick.Target, "60 m is within 1.5 x 40 m");
    }

    [TestMethod]
    public void Pick_InfantryFarBeyondTheArchers_YieldsTheArchers()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Archers, 40f, threat: false);
        pick.Consider(1, TargetClass.Infantry, 61f, threat: false);
        Assert.AreEqual(0, pick.Target);
    }

    [TestMethod]
    public void Pick_OnlyArchersLeft_TakesTheArchers()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Horse, 30f, threat: false);
        pick.Consider(1, TargetClass.Archers, 90f, threat: false);
        Assert.AreEqual(1, pick.Target);
    }

    [TestMethod]
    public void Pick_NoFootLeft_TakesTheNearestHorse()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Horse, 80f, threat: false);
        pick.Consider(1, TargetClass.Horse, 30f, threat: false);
        Assert.AreEqual(1, pick.Target);
    }

    [TestMethod]
    public void Pick_Nothing_IsNoTarget()
    {
        var pick = new TargetSelection();
        Assert.AreEqual(TargetSelection.None, pick.Target);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: false));
    }

    [TestMethod]
    public void CavalryThreat_ThreatHorseInsideTheDistance_IsAThreat_AndTheTargetStaysTheFoot()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Infantry, 120f, threat: false);
        pick.Consider(1, TargetClass.Horse, 95f, threat: true);
        Assert.IsTrue(pick.CavalryThreat(Tunables, braced: false));
        Assert.AreEqual(0, pick.Target, "the wall braces where it stands; it does not turn to chase the horse");
    }

    [TestMethod]
    public void CavalryThreat_ThreatHorseBeyondTheDistance_IsNotAThreat_UntilBraced()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Horse, 101f, threat: true);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: false), "not yet inside X");
        Assert.IsTrue(pick.CavalryThreat(Tunables, braced: true), "a braced wall keeps the square until they are beyond 1.5 X");
    }

    [TestMethod]
    public void CavalryThreat_BracedWall_ReleasesBeyondOneAndAHalfTimesTheDistance()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Horse, 151f, threat: true);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: true), "past 1.5 X the line forms back up");
        pick.Reset();
        pick.Consider(0, TargetClass.Horse, 150f, threat: true);
        Assert.IsTrue(pick.CavalryThreat(Tunables, braced: true));
    }

    [TestMethod]
    public void CavalryThreat_CloseHorseThatIsNoThreat_IsNotAThreat()
    {
        // The caller decides "threat": melee cavalry, significant, inbound or on us.
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Horse, 10f, threat: false);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: false));
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: true));
    }

    [TestMethod]
    public void Pick_NaNDistance_IsIgnored()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Infantry, float.NaN, threat: false);
        pick.Consider(1, TargetClass.Horse, float.NaN, threat: true);
        Assert.AreEqual(TargetSelection.None, pick.Target);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: false));
    }

    [TestMethod]
    public void Reset_ForgetsEverything()
    {
        var pick = new TargetSelection();
        pick.Consider(0, TargetClass.Infantry, 50f, threat: false);
        pick.Consider(1, TargetClass.Horse, 50f, threat: true);
        pick.Reset();
        Assert.AreEqual(TargetSelection.None, pick.Target);
        Assert.IsFalse(pick.CavalryThreat(Tunables, braced: true));
    }

    [TestMethod]
    public void Target_OnlyEverMovesToTheCandidateJustConsidered()
    {
        // EnemyScan keeps the Formation of the last candidate that took Target, so one walk
        // suffices; that needs Target never to jump back to an earlier index. The nearest
        // distances only fall as candidates arrive, so the archers-over-infantry test can only
        // flip on the candidate that moved a distance.
        var pick = new TargetSelection();
        var sequence = new (TargetClass cls, float d)[]
        {
            (TargetClass.Horse, 10f), (TargetClass.Infantry, 60f), (TargetClass.Archers, 30f), (TargetClass.Archers, 40f),
            (TargetClass.Infantry, 50f), (TargetClass.Archers, 20f), (TargetClass.Infantry, 25f), (TargetClass.Horse, 5f), (TargetClass.Infantry, 90f),
        };
        var last = TargetSelection.None;
        for (var i = 0; i < sequence.Length; i++)
        {
            pick.Consider(i, sequence[i].cls, sequence[i].d, threat: false);
            var now = pick.Target;
            Assert.IsTrue(now == last || now == i, $"after candidate {i} the target moved to {now}, not the candidate just considered");
            last = now;
        }
        Assert.AreEqual(6, pick.Target, "infantry at 25 m within 1.5 x the archers at 20 m");
    }

    [TestMethod]
    public void IsSignificant_NeedsFiveRidersAndATenthOfOurNumber()
    {
        Assert.IsFalse(CavalryThreat.IsSignificant(theirs: 1, ours: 150), "one rider does not re-form a wall");
        Assert.IsFalse(CavalryThreat.IsSignificant(4, 20));
        Assert.IsTrue(CavalryThreat.IsSignificant(5, 20));
        Assert.IsFalse(CavalryThreat.IsSignificant(14, 150), "a tenth of 150 is 15");
        Assert.IsTrue(CavalryThreat.IsSignificant(15, 150));
        Assert.IsTrue(CavalryThreat.IsSignificant(5, 0), "an empty wall is not a reason to ignore horse");
    }

    [TestMethod]
    public void IsOnUs_InsideContactDistance_WhateverTheVelocity()
    {
        Assert.IsTrue(CavalryThreat.IsOnUs(15f));
        Assert.IsFalse(CavalryThreat.IsOnUs(15.1f));
        Assert.IsFalse(CavalryThreat.IsOnUs(float.NaN));
    }

    [TestMethod]
    public void WorthGoing_HighGroundInsideTheCap_AndNobodyClose_IsWorthIt()
        => Assert.IsTrue(HighGroundRace.WorthGoing(highGroundDistance: 55f, closestEnemyFootDistance: 200f, in Tunables));

    [TestMethod]
    public void WorthGoing_HighGroundBeyondTheCap_IsNot()
        => Assert.IsFalse(HighGroundRace.WorthGoing(61f, 200f, in Tunables), "a hill 150 m away is a march, not a position");

    [TestMethod]
    public void WorthGoing_EnemyFootInsideTheHoldRadius_IsNot()
        => Assert.IsFalse(HighGroundRace.WorthGoing(20f, 50f, in Tunables), "the fight is here; horse are not passed in, the brace answers them");

    [TestMethod]
    public void WorthGoing_NoEnemyFootKnown_IsDecidedByTheCapAlone()
        => Assert.IsTrue(HighGroundRace.WorthGoing(20f, float.PositiveInfinity, in Tunables));

    [TestMethod]
    public void WorthGoing_NaN_Holds()
    {
        Assert.IsFalse(HighGroundRace.WorthGoing(float.NaN, 200f, in Tunables));
        Assert.IsFalse(HighGroundRace.WorthGoing(20f, float.NaN, in Tunables));
    }

    [TestMethod]
    public void ChargeWeight_MatchesVanillasInfantryCurve()
    {
        Assert.AreEqual(0.8f, ChargeWeight.Foot(10f, targetNotOnUs: false, targetIsHorse: false), 1e-4f, "ten seconds out");
        Assert.AreEqual(0.8f, ChargeWeight.Foot(30f, false, false), 1e-4f, "clamped beyond ten");
        Assert.AreEqual(0.9f, ChargeWeight.Foot(7f, false, false), 1e-4f, "halfway");
        Assert.AreEqual(1.2f, ChargeWeight.Foot(4f, false, false), 1e-4f, "four seconds: 1 times the go bonus");
        Assert.AreEqual(1.2f, ChargeWeight.Foot(2f, false, false), 1e-4f, "still inside the go window");
        Assert.AreEqual(1f, ChargeWeight.Foot(1f, false, false), 1e-4f, "under 1.5 s the go bonus is off, as vanilla");
        Assert.AreEqual(1.44f, ChargeWeight.Foot(3f, targetNotOnUs: true, false), 1e-4f, "a target looking elsewhere");
        Assert.AreEqual(0.6f, ChargeWeight.Foot(3f, false, targetIsHorse: true), 1e-4f, "horse at half, vanilla's class factor");
        Assert.AreEqual(0f, ChargeWeight.Foot(float.NaN, false, false));
    }

    [TestMethod]
    public void ChargeWeight_AtRowWeightOne_BeatsAnActiveWallOnlyWhenCloseAndTheTargetLooksElsewhere()
    {
        // FormationAI keeps the active behaviour unless a challenger beats it by 1.2x
        // (FormationAI.cs:182; 2.0x in the first five seconds). A wall row at 1 (BracedDefend or
        // BracedAdvance) with FootCharge at 1: the charge wins only inside the 1.5 to 4 s window
        // against a target that is not coming for us; a wall receiving a charge holds.
        const float sticky = 1.2f;
        Assert.IsTrue(ChargeWeight.Foot(3f, targetNotOnUs: true, targetIsHorse: false) * 1f > 1f * sticky, "close, target busy elsewhere: counter-charge");
        Assert.IsFalse(ChargeWeight.Foot(3f, targetNotOnUs: false, false) * 1f > 1f * sticky, "close, the target is on us: hold the wall");
        Assert.IsFalse(ChargeWeight.Foot(8f, true, false) * 1f > 1f * sticky, "far out the wall walks, it does not run");
        Assert.IsFalse(ChargeWeight.Foot(3f, true, false) * 0.3f > 1f * sticky, "a Defend-phase row at 0.3 never charges");
        Assert.IsFalse(ChargeWeight.Foot(3f, true, false) * 0.7f > 1f * sticky, "HitAndRun's 0.7 never displaces the active javelin dance");
        Assert.IsTrue(ChargeWeight.Foot(10f, false, false) * 0.7f > 0.5f, "but once the javelins are spent (the skirmish weighs 0 and the sticky goes with it) it beats the Advance row at 0.5 at any range");
    }
}
