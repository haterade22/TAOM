using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Towers heal only after a quiet spell, never above max, never once destroyed.</summary>
[TestClass]
public class TowerRegenTests
{
    private static readonly FordsTowerConfig Config = FordsMatchConfig.Default.Towers;

    private static (TowerRegen regen, TowerChain chain) Setup(FordsTowerConfig? config = null)
    {
        var cfg = config ?? Config;
        var chain = new TowerChain(FordsTestLayout.Create(), cfg);
        return (new TowerRegen(cfg, chain), chain);
    }

    private static void Hurt(TowerRegen regen, TowerChain chain, string id, int amount)
    {
        chain.ApplyDamage(id, amount);
        regen.NotifyDamaged(id);
    }

    [TestMethod]
    public void Tick_DuringTheDelayAfterDamage_HealsNothing()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);

        var heals = regen.Tick(19f);

        Assert.AreEqual(0, heals.Count);
        Assert.AreEqual(Config.TowerHitPoints - 1000, chain.HitPoints(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void Tick_AfterTheDelay_HealsPerSecond()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);
        regen.Tick(20f);

        var heals = regen.Tick(2f);

        Assert.AreEqual(1, heals.Count);
        Assert.AreEqual(FordsTestLayout.EastOuter0, heals[0].TowerId);
        Assert.AreEqual(30, heals[0].Amount);
        Assert.AreEqual(Config.TowerHitPoints - 1000 + 30, chain.HitPoints(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void Tick_DelayCrossedInsideOneStep_CountsOnlyTheTimeAfterIt()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);

        var heals = regen.Tick(22f);

        Assert.AreEqual(30, heals[0].Amount);
    }

    [TestMethod]
    public void Tick_FractionalRegen_CarriesOverBetweenTicks()
    {
        var (regen, chain) = Setup(Config with { RegenPerSecond = 1f });
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 100);
        regen.Tick(20f);

        var first = regen.Tick(0.5f);
        var second = regen.Tick(0.5f);

        Assert.AreEqual(0, first.Count);
        Assert.AreEqual(1, second[0].Amount);
    }

    [TestMethod]
    public void NotifyDamaged_AfterRegenStarted_RestartsTheDelay()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);
        regen.Tick(25f);

        regen.NotifyDamaged(FordsTestLayout.EastOuter0);
        var heals = regen.Tick(10f);

        Assert.AreEqual(0, heals.Count);
    }

    [TestMethod]
    public void Tick_NearlyFull_NeverHealsAboveMax()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 10);
        regen.Tick(20f);

        var heals = regen.Tick(60f);

        Assert.AreEqual(10, heals[0].Amount);
        Assert.AreEqual(Config.TowerHitPoints, chain.HitPoints(FordsTestLayout.EastOuter0));
        Assert.AreEqual(0, regen.Tick(10f).Count);
    }

    [TestMethod]
    public void Tick_DestroyedTower_NeverHeals()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        var heals = regen.Tick(100f);

        Assert.AreEqual(0, heals.Count);
        Assert.AreEqual(0, chain.HitPoints(FordsTestLayout.EastOuter0));
    }

    [TestMethod]
    public void Tick_UntouchedFullTowers_ReportNothing()
    {
        var (regen, _) = Setup();

        Assert.AreEqual(0, regen.Tick(100f).Count);
    }

    [TestMethod]
    public void Tick_TowerDamagedBeforeAnyTick_HealsOnceTheDelayHasPassed()
    {
        var (regen, chain) = Setup();
        chain.ApplyDamage(FordsTestLayout.WestKeep, 500);
        regen.NotifyDamaged(FordsTestLayout.WestKeep);

        regen.Tick(20f);
        var heals = regen.Tick(1f);

        Assert.AreEqual(15, heals[0].Amount);
    }

    [TestMethod]
    public void NotifyDamaged_UnknownTower_IsIgnored()
    {
        var (regen, _) = Setup();

        regen.NotifyDamaged("barn");

        Assert.AreEqual(0, regen.Tick(100f).Count);
    }

    [TestMethod]
    public void Tick_NonPositiveStep_HealsNothing()
    {
        var (regen, chain) = Setup();
        Hurt(regen, chain, FordsTestLayout.EastOuter0, 1000);
        regen.Tick(30f);

        Assert.AreEqual(0, regen.Tick(0f).Count);
        Assert.AreEqual(0, regen.Tick(-1f).Count);
    }
}
