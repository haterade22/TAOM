using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Volley cooldown, target choice (heroes first, then nearest) and the range and liveness filters.</summary>
[TestClass]
public class TowerFireRulesTests
{
    private static readonly FordsTowerConfig Config = FordsMatchConfig.Default.Towers;

    private static (TowerFireRules rules, TowerChain chain) Setup()
    {
        var chain = new TowerChain(FordsTestLayout.Create(), Config);
        return (new TowerFireRules(Config, chain), chain);
    }

    private static TargetCandidate Foe(int key, float distance, bool hero = false) => new(new AgentKey(key), distance, hero);

    private static IReadOnlyList<TowerTargets> Targets(string tower, params TargetCandidate[] foes) =>
        new[] { new TowerTargets(tower, foes) };

    [TestMethod]
    public void Tick_EnemyInRange_FiresAVolleyOfConfiguredMissiles()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(0.1f, Targets(FordsTestLayout.EastOuter0, Foe(7, 30f)));

        Assert.AreEqual(1, volleys.Count);
        Assert.AreEqual(FordsTestLayout.EastOuter0, volleys[0].TowerId);
        Assert.AreEqual(new AgentKey(7), volleys[0].Target);
        Assert.AreEqual(Config.VolleyMissiles, volleys[0].Missiles);
    }

    [TestMethod]
    public void Tick_NoEnemies_FiresNothing()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Tick(1f, Targets(FordsTestLayout.EastOuter0)).Count);
    }

    [TestMethod]
    public void Tick_NoTargetEntries_FiresNothing()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Tick(1f, new List<TowerTargets>()).Count);
    }

    [TestMethod]
    public void Tick_EnemyBeyondFireRange_IsIgnored()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(7, Config.FireRange + 0.5f))).Count);
    }

    [TestMethod]
    public void Tick_EnemyExactlyAtFireRange_IsTargeted()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(1, rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(7, Config.FireRange))).Count);
    }

    [TestMethod]
    public void Tick_SeveralEnemies_PicksTheNearest()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(1, 40f), Foe(2, 12f), Foe(3, 25f)));

        Assert.AreEqual(new AgentKey(2), volleys[0].Target);
    }

    [TestMethod]
    public void Tick_HeroInRange_IsPickedOverANearerSoldier()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(1, 5f), Foe(2, 40f, hero: true)));

        Assert.AreEqual(new AgentKey(2), volleys[0].Target);
    }

    [TestMethod]
    public void Tick_SeveralHeroes_PicksTheNearestHero()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(1, 30f, hero: true), Foe(2, 10f, hero: true), Foe(3, 2f)));

        Assert.AreEqual(new AgentKey(2), volleys[0].Target);
    }

    [TestMethod]
    public void Tick_HeroBeyondRange_DoesNotOverrideASoldierInRange()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(1, 20f), Foe(2, Config.FireRange + 10f, hero: true)));

        Assert.AreEqual(new AgentKey(1), volleys[0].Target);
    }

    [TestMethod]
    public void Tick_DistanceTie_KeepsTheFirstListed()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(4, 15f), Foe(5, 15f)));

        Assert.AreEqual(new AgentKey(4), volleys[0].Target);
    }

    [TestMethod]
    public void Tick_BeforeTheCooldownEnds_DoesNotFireAgain()
    {
        var (rules, _) = Setup();
        var targets = Targets(FordsTestLayout.EastOuter0, Foe(7, 30f));
        rules.Tick(0.1f, targets);

        var volleys = rules.Tick(Config.VolleyIntervalSeconds - 0.5f, targets);

        Assert.AreEqual(0, volleys.Count);
    }

    [TestMethod]
    public void Tick_AfterTheCooldownEnds_FiresAgain()
    {
        var (rules, _) = Setup();
        var targets = Targets(FordsTestLayout.EastOuter0, Foe(7, 30f));
        rules.Tick(0.1f, targets);
        rules.Tick(Config.VolleyIntervalSeconds - 0.5f, targets);

        var volleys = rules.Tick(0.5f, targets);

        Assert.AreEqual(1, volleys.Count);
    }

    [TestMethod]
    public void Tick_CooldownRunsWhileNoEnemyIsInRange()
    {
        var (rules, _) = Setup();
        rules.Tick(0.1f, Targets(FordsTestLayout.EastOuter0, Foe(7, 30f)));
        rules.Tick(Config.VolleyIntervalSeconds, Targets(FordsTestLayout.EastOuter0));

        var volleys = rules.Tick(0.1f, Targets(FordsTestLayout.EastOuter0, Foe(8, 30f)));

        Assert.AreEqual(1, volleys.Count);
    }

    [TestMethod]
    public void Tick_TwoTowers_KeepSeparateCooldowns()
    {
        var (rules, _) = Setup();
        rules.Tick(0.1f, Targets(FordsTestLayout.EastOuter0, Foe(7, 30f)));

        var both = new[]
        {
            new TowerTargets(FordsTestLayout.EastOuter0, new[] { Foe(7, 30f) }),
            new TowerTargets(FordsTestLayout.EastOuter1, new[] { Foe(9, 20f) }),
        };
        var volleys = rules.Tick(0.1f, both);

        Assert.AreEqual(1, volleys.Count);
        Assert.AreEqual(FordsTestLayout.EastOuter1, volleys[0].TowerId);
    }

    [TestMethod]
    public void Tick_DestroyedTower_NeverFires()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.AreEqual(0, rules.Tick(1f, Targets(FordsTestLayout.EastOuter0, Foe(7, 5f))).Count);
    }

    [TestMethod]
    public void Tick_UnknownTower_IsSkipped()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Tick(1f, Targets("barn", Foe(7, 5f))).Count);
    }

    [TestMethod]
    public void Tick_LockedTower_StillFires()
    {
        var (rules, chain) = Setup();
        Assert.IsTrue(chain.IsLocked(FordsTestLayout.EastInner0));

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.EastInner0, Foe(7, 5f)));

        Assert.AreEqual(1, volleys.Count);
    }

    [TestMethod]
    public void Tick_KeepFires()
    {
        var (rules, _) = Setup();

        var volleys = rules.Tick(1f, Targets(FordsTestLayout.WestKeep, Foe(7, 5f)));

        Assert.AreEqual(FordsTestLayout.WestKeep, volleys.Single().TowerId);
    }
}
