using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The weight each TAOM tactic reports to <c>TeamAIComponent.MakeDecision</c>, as a pure
/// function of a <see cref="TeamQuerySnapshot"/>. Pinned properties: 0 in every documented
/// no-go case, monotonic in the ratio the doctrine is about, clamped where the input can run
/// away, and never NaN or infinite for any finite or non-finite input (a NaN weight would win or
/// lose <c>MaxBy</c> unpredictably on the AI thread).
/// </summary>
[TestClass]
public class DoctrineWeightsTests
{
    private static TeamQuerySnapshot Snapshot(
        int members = 200, int enemies = 200, float infantry = 0.6f, float ranged = 0.2f, float cavalry = 0.15f,
        float rangedCavalry = 0.05f, float remainingPower = 1f, float notEngagingAdvantage = 1f,
        bool hasInfantry = true, bool hasArchers = true, bool hasCavalry = true, bool defenseApplicable = true,
        bool ringFits = true, bool isDefender = true, int? infantryCount = null, float throwing = 0f, bool hasVanguard = false, int? infantryTotal = null)
        => new TeamQuerySnapshot(members, enemies, infantry, ranged, cavalry, rangedCavalry, remainingPower,
            notEngagingAdvantage, hasInfantry, hasArchers, hasCavalry, defenseApplicable, ringFits, isDefender,
            infantryCount ?? (int)(members * infantry), throwing, hasVanguard, infantryTotal ?? infantryCount ?? (int)(members * infantry));

    private static float[] Every(TeamQuerySnapshot s) => new[]
    {
        DoctrineWeights.ShieldWall(s), DoctrineWeights.InfantryMass(s), DoctrineWeights.CavalryDominance(s), DoctrineWeights.ArcherRing(s),
        DoctrineWeights.TwoLineWall(s), DoctrineWeights.Envelop(s), DoctrineWeights.DisciplinedLine(s), DoctrineWeights.ArcherAdvance(s),
        DoctrineWeights.EoredScreen(s), DoctrineWeights.HitAndRun(s), DoctrineWeights.MumakVanguard(s),
    };

    [TestMethod]
    public void ShieldWall_NoInfantry_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.ShieldWall(Snapshot(hasInfantry: false)));

    [TestMethod]
    public void ShieldWall_GrowsWithInfantryAndRangedShare()
    {
        var mostlyCavalry = DoctrineWeights.ShieldWall(Snapshot(infantry: 0.2f, ranged: 0.1f, cavalry: 0.7f, rangedCavalry: 0f));
        var mostlyInfantry = DoctrineWeights.ShieldWall(Snapshot(infantry: 0.7f, ranged: 0.25f, cavalry: 0.05f, rangedCavalry: 0f));

        Assert.IsTrue(mostlyInfantry > mostlyCavalry);
    }

    [TestMethod]
    public void ShieldWall_IsNotGatedOnDefenseApplicable()
    {
        // The whole point of the TAOM tactic: vanilla's defensive tactics return 0 for any
        // attacker (IsDefenseApplicable is false off the Defender side), so a Dwarven army that
        // attacks charges. This one holds either way.
        Assert.IsTrue(DoctrineWeights.ShieldWall(Snapshot(defenseApplicable: false, isDefender: false)) > 0f);
    }

    [TestMethod]
    public void ShieldWall_AtFullStrength_BeatsANeutralChargeOfThirtyPercent()
    {
        // Erebor ships Charge*0.3 beside ShieldWall*1.0; a wall that could not out-weigh a
        // one-third charge at full strength would never be chosen.
        var wall = DoctrineWeights.ShieldWall(Snapshot(infantry: 0.7f, ranged: 0.2f, cavalry: 0.1f, rangedCavalry: 0f));
        Assert.IsTrue(wall > 0.9f, $"wall weight {wall}");
    }

    [TestMethod]
    public void InfantryMass_NoInfantry_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.InfantryMass(Snapshot(hasInfantry: false)));

    [TestMethod]
    public void InfantryMass_GrowsWithNumbers_AndClampsAtTwoToOne()
    {
        var even = DoctrineWeights.InfantryMass(Snapshot(members: 200, enemies: 200));
        var double_ = DoctrineWeights.InfantryMass(Snapshot(members: 400, enemies: 200));
        var quadruple = DoctrineWeights.InfantryMass(Snapshot(members: 800, enemies: 200));

        Assert.IsTrue(double_ > even);
        Assert.AreEqual(double_, quadruple, 1e-6f, "numbers past 2:1 buy nothing more");
    }

    [TestMethod]
    public void InfantryMass_Outnumbered_FallsToHalfButNotBelow()
    {
        var even = DoctrineWeights.InfantryMass(Snapshot(members: 200, enemies: 200));
        var outnumbered = DoctrineWeights.InfantryMass(Snapshot(members: 50, enemies: 200));

        Assert.AreEqual(even * 0.5f, outnumbered, 1e-6f);
    }

    [TestMethod]
    public void InfantryMass_NoEnemies_DoesNotDivideByZero()
    {
        var weight = DoctrineWeights.InfantryMass(Snapshot(members: 200, enemies: 0));

        Assert.IsFalse(float.IsNaN(weight) || float.IsInfinity(weight));
    }

    [TestMethod]
    public void CavalryDominance_NoCavalry_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.CavalryDominance(Snapshot(hasCavalry: false)));

    [TestMethod]
    public void CavalryDominance_GrowsWithCavalryShare()
    {
        var light = DoctrineWeights.CavalryDominance(Snapshot(infantry: 0.7f, ranged: 0.2f, cavalry: 0.1f, rangedCavalry: 0f));
        var heavy = DoctrineWeights.CavalryDominance(Snapshot(infantry: 0.3f, ranged: 0.1f, cavalry: 0.6f, rangedCavalry: 0f));

        Assert.IsTrue(heavy > light);
    }

    [TestMethod]
    public void CavalryDominance_AllRangedCavalry_DoesNotDivideByZero()
    {
        var weight = DoctrineWeights.CavalryDominance(Snapshot(infantry: 0f, ranged: 0f, cavalry: 0f, rangedCavalry: 1f));

        Assert.IsFalse(float.IsNaN(weight) || float.IsInfinity(weight));
    }

    [TestMethod]
    public void ArcherRing_Attacker_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.ArcherRing(Snapshot(isDefender: false)));

    [TestMethod]
    public void ArcherRing_NoArchersOrNoInfantry_IsZero()
    {
        Assert.AreEqual(0f, DoctrineWeights.ArcherRing(Snapshot(hasArchers: false)));
        Assert.AreEqual(0f, DoctrineWeights.ArcherRing(Snapshot(hasInfantry: false)));
    }

    [TestMethod]
    public void ArcherRing_RingTooSmallForTheArchers_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.ArcherRing(Snapshot(ringFits: false)));

    [TestMethod]
    public void ArcherRing_OutShot_IsZero()
        => Assert.AreEqual(0f, DoctrineWeights.ArcherRing(Snapshot(defenseApplicable: false)), "vanilla's IsDefenseApplicable rule: a ring that is being out-shot is a target, not a defence");

    [TestMethod]
    public void ArcherRing_GrowsWithTheSmallerOfInfantryAndRangedShare()
    {
        var thin = DoctrineWeights.ArcherRing(Snapshot(infantry: 0.8f, ranged: 0.1f, cavalry: 0.1f, rangedCavalry: 0f));
        var balanced = DoctrineWeights.ArcherRing(Snapshot(infantry: 0.5f, ranged: 0.4f, cavalry: 0.1f, rangedCavalry: 0f));

        Assert.IsTrue(balanced > thin);
    }

    [TestMethod]
    public void EveryWeight_LowRemainingPower_StaysFinite()
    {
        foreach (var w in Every(Snapshot(remainingPower: 0f, infantryCount: 300, throwing: 0.6f, hasVanguard: true)))
            Assert.IsFalse(float.IsNaN(w) || float.IsInfinity(w));
    }

    [TestMethod]
    public void EveryWeight_NaNInput_IsZeroNotNaN()
    {
        var s = Snapshot(infantry: float.NaN, remainingPower: float.NaN, notEngagingAdvantage: float.NaN, throwing: float.NaN, hasVanguard: true);

        foreach (var w in Every(s))
            Assert.AreEqual(0f, w);
    }

    [TestMethod]
    public void TwoLineWall_NeedsTheMenForTwoLines_ThenBeatsTheSingleWallByTheStickyFactor()
    {
        Assert.AreEqual(0f, DoctrineWeights.TwoLineWall(Snapshot(infantryTotal: DoctrineWeights.TwoLineMinInfantry - 1)));
        var s = Snapshot(infantryTotal: DoctrineWeights.TwoLineMinInfantry);
        Assert.IsTrue(DoctrineWeights.TwoLineWall(s) > DoctrineWeights.ShieldWall(s) * 1.5f, "an edge under the engine's 1.5x could never take the team back from the plain wall");
    }

    [TestMethod]
    public void TwoLineWall_GateReadsTheClassTotal_NotTheLargestFormationItsOwnSplitHalves()
    {
        // 120 foot: after the 2/1/2/1 split the largest formation holds 60; the gate must still pass.
        var afterSplit = Snapshot(infantryCount: 60, infantryTotal: 120);
        Assert.IsTrue(DoctrineWeights.TwoLineWall(afterSplit) > 0f);
        var afterThirds = Snapshot(members: 300, enemies: 200, infantryCount: 50, infantryTotal: 150);
        Assert.IsTrue(DoctrineWeights.Envelop(afterThirds) > 0f);
    }

    [TestMethod]
    public void Envelop_NeedsNumbersAndBodies_ThenBeatsTheMassByTheStickyFactor()
    {
        Assert.AreEqual(0f, DoctrineWeights.Envelop(Snapshot(members: 200, enemies: 200, infantryTotal: 150)), "at parity the horde does not spread three ways");
        Assert.AreEqual(0f, DoctrineWeights.Envelop(Snapshot(members: 300, enemies: 200, infantryTotal: DoctrineWeights.EnvelopMinInfantry - 1)));
        var s = Snapshot(members: 300, enemies: 200, infantryTotal: 200);
        Assert.IsTrue(DoctrineWeights.Envelop(s) > DoctrineWeights.InfantryMass(s) * 1.5f);
        Assert.AreEqual(0f, DoctrineWeights.Envelop(Snapshot(members: 300, enemies: 0, infantryTotal: 200, infantry: float.NaN)));
    }

    [TestMethod]
    public void GatedEdge_ExceedsTheEnginesStickyFactor()
        => Assert.IsTrue(DoctrineWeights.GatedEdge > 1.5f);

    [TestMethod]
    public void DisciplinedLine_BothSides_GrowsWithFootAndBow()
    {
        Assert.IsTrue(DoctrineWeights.DisciplinedLine(Snapshot(isDefender: false)) > 0f);
        Assert.IsTrue(DoctrineWeights.DisciplinedLine(Snapshot(infantry: 0.6f, ranged: 0.3f)) > DoctrineWeights.DisciplinedLine(Snapshot(infantry: 0.3f, ranged: 0.1f, cavalry: 0.6f)));
        Assert.AreEqual(0f, DoctrineWeights.DisciplinedLine(Snapshot(hasInfantry: false)));
    }

    [TestMethod]
    public void ArcherAdvance_IsTheAttackersDoctrine_AndNeedsBothArms()
    {
        Assert.AreEqual(0f, DoctrineWeights.ArcherAdvance(Snapshot(isDefender: true)));
        Assert.AreEqual(0f, DoctrineWeights.ArcherAdvance(Snapshot(isDefender: false, hasArchers: false)));
        Assert.AreEqual(0f, DoctrineWeights.ArcherAdvance(Snapshot(isDefender: false, hasInfantry: false)));
        Assert.IsTrue(DoctrineWeights.ArcherAdvance(Snapshot(isDefender: false, infantry: 0.45f, ranged: 0.45f)) > DoctrineWeights.ArcherAdvance(Snapshot(isDefender: false, infantry: 0.8f, ranged: 0.1f)));
    }

    [TestMethod]
    public void EoredScreen_IsTheDefendersDoctrine_AboveTheDominanceCharge()
    {
        Assert.AreEqual(0f, DoctrineWeights.EoredScreen(Snapshot(isDefender: false)));
        Assert.AreEqual(0f, DoctrineWeights.EoredScreen(Snapshot(hasCavalry: false)));
        var rohan = Snapshot(infantry: 0.3f, ranged: 0.1f, cavalry: 0.5f, rangedCavalry: 0.1f);
        Assert.IsTrue(DoctrineWeights.EoredScreen(rohan) > DoctrineWeights.CavalryDominance(rohan), "a defending eored screens first");
    }

    [TestMethod]
    public void HitAndRun_ScalesWithTheThrowingShare_AndIsNothingWithoutJavelins()
    {
        Assert.AreEqual(0f, DoctrineWeights.HitAndRun(Snapshot(throwing: 0f)));
        Assert.IsTrue(DoctrineWeights.HitAndRun(Snapshot(throwing: 0.25f)) < DoctrineWeights.HitAndRun(Snapshot(throwing: 0.5f)));
        Assert.AreEqual(DoctrineWeights.HitAndRun(Snapshot(throwing: 0.5f)), DoctrineWeights.HitAndRun(Snapshot(throwing: 1f)), "half the line throwing is the full doctrine");
        Assert.AreEqual(0f, DoctrineWeights.HitAndRun(Snapshot(throwing: float.NaN)));
    }

    [TestMethod]
    public void MumakVanguard_NeedsARoutedVanguard()
    {
        Assert.AreEqual(0f, DoctrineWeights.MumakVanguard(Snapshot(hasVanguard: false)));
        Assert.IsTrue(DoctrineWeights.MumakVanguard(Snapshot(hasVanguard: true)) > 0f);
        Assert.AreEqual(0f, DoctrineWeights.MumakVanguard(Snapshot(hasVanguard: true, hasInfantry: false)));
    }

    [TestMethod]
    public void RingGeometry_Fits_MatchesVanillaCircumferenceRule()
    {
        // TacticDefensiveRing.GetTacticWeight: ring circumference from the infantry count against
        // the side of the archers' square. 100 infantry at 1 m interval + 0.74 m diameter ring a
        // 27.7 m circumference; 25 archers form a 5 x 5 square about 4.7 m wide. Fits.
        Assert.IsTrue(RingGeometry.Fits(infantryCount: 100, infantryMaxInterval: 1f, infantryUnitDiameter: 0.74f, archerCount: 25, archerUnitDiameter: 0.74f, archerInterval: 0.25f));
        // 12 infantry cannot ring 100 archers.
        Assert.IsFalse(RingGeometry.Fits(12, 1f, 0.74f, 100, 0.74f, 0.25f));
        Assert.IsFalse(RingGeometry.Fits(0, 1f, 0.74f, 1, 0.74f, 0.25f));
    }
}
