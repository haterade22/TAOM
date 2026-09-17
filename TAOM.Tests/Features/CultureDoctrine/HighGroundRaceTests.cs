using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Whether a formation can reach the high ground and form up before the enemy's foot gets
/// there. Vanilla never computes this: <c>TacticDefensiveEngagement</c> only lowers its weight
/// when the high ground is far, and <c>BehaviorHoldHighGround</c> tracks then locks. A wall that
/// loses the race is caught on the march; one that knows it will lose forms where it stands.
/// Cavalry is deliberately not a racer: a wall that forms at the last moment still receives a
/// cavalry charge in a wall.
/// </summary>
[TestClass]
public class HighGroundRaceTests
{
    private static readonly RaceTunables Tunables = new RaceTunables(formUpSeconds: 6f, formUpSecondsPerUnit: 0.03f, marginSeconds: 3f);

    [TestMethod]
    public void Decide_NoEnemyFoot_Goes()
    {
        Assert.IsTrue(HighGroundRace.Decide(ourDistance: 120f, ourSpeed: 3f, ourUnits: 200, Tunables, new float[0]));
    }

    [TestMethod]
    public void Decide_EnemyArrivesAfterWeHaveFormed_Goes()
    {
        // Us: 120 m at 3 m/s = 40 s, plus 6 + 200 * 0.03 = 12 s form-up, plus 3 s margin = 55 s.
        Assert.IsTrue(HighGroundRace.Decide(120f, 3f, 200, Tunables, new[] { 60f }));
    }

    [TestMethod]
    public void Decide_EnemyArrivesBeforeWeHaveFormed_Holds()
    {
        Assert.IsFalse(HighGroundRace.Decide(120f, 3f, 200, Tunables, new[] { 50f }));
    }

    [TestMethod]
    public void Decide_TheEarliestEnemyDecides()
    {
        Assert.IsFalse(HighGroundRace.Decide(120f, 3f, 200, Tunables, new[] { 90f, 50f, 80f }));
    }

    [TestMethod]
    public void Decide_MarginIsTheTieBreaker()
    {
        var noMargin = new RaceTunables(6f, 0.03f, marginSeconds: 0f);
        // 40 + 12 = 52 s exactly against an enemy at 52 s: without margin a tie goes; with 3 s it holds.
        Assert.IsTrue(HighGroundRace.Decide(120f, 3f, 200, noMargin, new[] { 52.5f }));
        Assert.IsFalse(HighGroundRace.Decide(120f, 3f, 200, Tunables, new[] { 52.5f }));
    }

    [TestMethod]
    public void Decide_FormUpGrowsWithUnitCount()
    {
        // 50 men form in 7.5 s; 400 men in 18 s. Same march, same enemy at 54 s.
        Assert.IsTrue(HighGroundRace.Decide(120f, 3f, 50, Tunables, new[] { 54f }));
        Assert.IsFalse(HighGroundRace.Decide(120f, 3f, 400, Tunables, new[] { 54f }));
    }

    [TestMethod]
    public void Decide_AlreadyThere_Goes()
    {
        Assert.IsTrue(HighGroundRace.Decide(0f, 3f, 200, Tunables, new[] { 5f }), "no march, and forming where we already stand is what holding means anyway");
    }

    [TestMethod]
    public void Decide_OurSpeedZeroOrNotFinite_Holds()
    {
        Assert.IsFalse(HighGroundRace.Decide(120f, 0f, 200, Tunables, new[] { 60f }));
        Assert.IsFalse(HighGroundRace.Decide(120f, float.NaN, 200, Tunables, new[] { 60f }));
        Assert.IsFalse(HighGroundRace.Decide(float.NaN, 3f, 200, Tunables, new[] { 60f }));
    }

    [TestMethod]
    public void Decide_NonFiniteEnemyEta_IsIgnoredNotFatal()
    {
        // A NaN or infinite ETA (zero speed on their side) cannot beat us; it is not a racer.
        Assert.IsTrue(HighGroundRace.Decide(120f, 3f, 200, Tunables, new[] { float.NaN, float.PositiveInfinity, 60f }));
    }

    [TestMethod]
    public void Eta_IsDistanceOverSpeed_AndInfiniteForAStoppedFormation()
    {
        Assert.AreEqual(40f, HighGroundRace.Eta(120f, 3f), 1e-5f);
        Assert.IsTrue(float.IsInfinity(HighGroundRace.Eta(120f, 0f)));
        Assert.IsTrue(float.IsNaN(HighGroundRace.Eta(float.NaN, 3f)));
    }

    [TestMethod]
    public void ShieldWallDefenderAndArcherRing_CarryRaceTunables()
    {
        Assert.IsTrue(DoctrinePlans.ShieldWallDefender.Race.FormUpSeconds > 0f);
        Assert.IsTrue(DoctrinePlans.ArcherRing.Race.FormUpSeconds > 0f);
        Assert.IsTrue(DoctrinePlans.ArcherRing.Race.FormUpSeconds >= DoctrinePlans.ShieldWallDefender.Race.FormUpSeconds, "a ring around a square takes longer to form than a line");
    }
}
