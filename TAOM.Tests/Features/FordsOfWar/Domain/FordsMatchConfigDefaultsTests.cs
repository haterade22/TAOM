using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// The built-in defaults are the numbers in build-spec section 5. Later stages build test matches
/// from them, so a drift here is caught at the source.
/// </summary>
[TestClass]
public class FordsMatchConfigDefaultsTests
{
    private static readonly FordsMatchConfig C = FordsMatchConfig.Default;

    [TestMethod]
    public void Default_Economy_MatchesSpec()
    {
        Assert.AreEqual(600, C.Economy.StartingGold);
        Assert.AreEqual(120, C.Economy.IncomePerMinute);
        Assert.AreEqual(0.35f, C.Economy.KillBountyFraction);
        Assert.AreEqual(2, C.Economy.MinKillBounty);
        Assert.AreEqual(300, C.Economy.TowerBounty);
        Assert.AreEqual(250, C.Economy.HeroKillBounty);
        Assert.AreEqual(60, C.Economy.CaptureIncomePerMinute);
        Assert.AreEqual(99999, C.Economy.MaxGold);
    }

    [TestMethod]
    public void Default_Waves_MatchSpec()
    {
        Assert.AreEqual(40f, C.Waves.FreeWaveIntervalSeconds);
        Assert.AreEqual(90, C.Waves.MaxLivePerLanePerSide);
        Assert.AreEqual(300, C.Waves.MaxLivePerSide);
        Assert.AreEqual(6, C.Waves.MaxSpawnsPerTick);
        Assert.AreEqual(3, C.Waves.MaxQueuedPacksPerLane);
    }

    [TestMethod]
    public void Default_Towers_MatchSpec()
    {
        Assert.AreEqual(8000, C.Towers.TowerHitPoints);
        Assert.AreEqual(11000, C.Towers.InnerTowerHitPoints);
        Assert.AreEqual(18000, C.Towers.KeepHitPoints);
        Assert.AreEqual(0.15f, C.Towers.RangedDamageFactor);
        Assert.AreEqual(4f, C.Towers.HeroDamageFactor);
        Assert.AreEqual(3f, C.Towers.SiegeDamageFactor);
        Assert.AreEqual(15f, C.Towers.RegenPerSecond);
        Assert.AreEqual(20f, C.Towers.RegenDelaySeconds);
        Assert.AreEqual(2, C.Towers.GunnersPerTower);
        Assert.AreEqual(4f, C.Towers.VolleyIntervalSeconds);
        Assert.AreEqual(3, C.Towers.VolleyMissiles);
        Assert.AreEqual(45f, C.Towers.FireRange);
        Assert.AreEqual(20f, C.Towers.AttackRange);
    }

    [TestMethod]
    public void Default_HeroesLanesCapture_MatchSpec()
    {
        Assert.AreEqual(2, C.Heroes.MaxAlivePerSide);
        Assert.AreEqual(60f, C.Heroes.RespawnBaseSeconds);
        Assert.AreEqual(6f, C.Heroes.RespawnPerMinuteSeconds);
        Assert.AreEqual(0.5f, C.Heroes.BuybackBaseFraction);
        Assert.AreEqual(0.05f, C.Heroes.BuybackPerMinuteFraction);
        Assert.AreEqual(20f, C.Lanes.OrderCooldownSeconds);
        Assert.AreEqual(25f, C.Lanes.EngageRange);
        Assert.AreEqual(6f, C.Lanes.WaypointReachRadius);
        Assert.AreEqual(60f, C.Lanes.HoldDistanceBeforeTower);
        Assert.AreEqual(40, C.Lanes.AssaultThreshold);
        Assert.AreEqual(12f, C.Capture.Radius);
        Assert.AreEqual(20f, C.Capture.SecondsToCapture);
        Assert.AreEqual(10f, C.CountdownSeconds);
    }

    [TestMethod]
    public void Default_AiProfiles_MatchSpec()
    {
        Assert.AreEqual(new FordsAiProfile("easy", 12f, 0.8f, 0.25f, 200), C.AiProfiles["easy"]);
        Assert.AreEqual(new FordsAiProfile("normal", 8f, 1.0f, 0.1f, 300), C.AiProfiles["normal"]);
        Assert.AreEqual(new FordsAiProfile("hard", 5f, 1.2f, 0.0f, 400), C.AiProfiles["hard"]);
    }
}
