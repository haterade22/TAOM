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
        bool ringFits = true, bool isDefender = true)
        => new TeamQuerySnapshot(members, enemies, infantry, ranged, cavalry, rangedCavalry, remainingPower,
            notEngagingAdvantage, hasInfantry, hasArchers, hasCavalry, defenseApplicable, ringFits, isDefender);

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
        var s = Snapshot(remainingPower: 0f);

        foreach (var w in new[] { DoctrineWeights.ShieldWall(s), DoctrineWeights.InfantryMass(s), DoctrineWeights.CavalryDominance(s), DoctrineWeights.ArcherRing(s) })
            Assert.IsFalse(float.IsNaN(w) || float.IsInfinity(w));
    }

    [TestMethod]
    public void EveryWeight_NaNInput_IsZeroNotNaN()
    {
        var s = Snapshot(infantry: float.NaN, remainingPower: float.NaN, notEngagingAdvantage: float.NaN);

        Assert.AreEqual(0f, DoctrineWeights.ShieldWall(s));
        Assert.AreEqual(0f, DoctrineWeights.InfantryMass(s));
        Assert.AreEqual(0f, DoctrineWeights.CavalryDominance(s));
        Assert.AreEqual(0f, DoctrineWeights.ArcherRing(s));
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
