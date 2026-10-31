using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The damage table: locks, ranged, hero and siege factors, and the one-point floor.</summary>
[TestClass]
public class TowerDamageRulesTests
{
    private static readonly FordsTowerConfig Config = FordsMatchConfig.Default.Towers;

    private static (TowerDamageRules rules, TowerChain chain) Setup()
    {
        var chain = new TowerChain(FordsTestLayout.Create(), Config);
        return (new TowerDamageRules(Config, chain), chain);
    }

    private static TowerHit Hit(string id, UnitRole role, int raw, bool ranged = false) => new(id, ranged, role, raw);

    [TestMethod]
    public void Effective_UnlockedMeleeHit_PassesThroughUnchanged()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(40, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Infantry, 40)));
    }

    [TestMethod]
    public void Effective_LockedTower_IsZero()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Effective(Hit(FordsTestLayout.EastInner0, UnitRole.Infantry, 400)));
    }

    [TestMethod]
    public void Effective_KeepBeforeALaneFalls_IsZero()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Effective(Hit(FordsTestLayout.EastKeep, UnitRole.Hero, 400)));
    }

    [TestMethod]
    public void Effective_InnerTowerAfterOuterFalls_TakesDamage()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.AreEqual(25, rules.Effective(Hit(FordsTestLayout.EastInner0, UnitRole.Spear, 25)));
    }

    [TestMethod]
    public void Effective_RangedHit_ScalesByRangedFactor()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(30, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Ranged, 200, ranged: true)));
    }

    [TestMethod]
    public void Effective_HeroHit_ScalesByHeroFactor()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(160, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Hero, 40)));
    }

    [TestMethod]
    public void Effective_SiegeUnitHit_ScalesBySiegeFactor()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(120, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Siege, 40)));
    }

    [TestMethod]
    public void Effective_RangedHero_StacksBothFactors()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(120, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Hero, 200, ranged: true)));
    }

    [TestMethod]
    public void Effective_TinyRangedHit_IsAtLeastOne()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(1, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Ranged, 2, ranged: true)));
    }

    [TestMethod]
    public void Effective_ScaledValueRoundsToNearest()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(2, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Ranged, 10, ranged: true)));
    }

    [TestMethod]
    public void Effective_NonPositiveDamage_IsZero()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Infantry, 0)));
        Assert.AreEqual(0, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Infantry, -30)));
    }

    [TestMethod]
    public void Effective_UnknownTower_IsZero()
    {
        var (rules, _) = Setup();

        Assert.AreEqual(0, rules.Effective(Hit("barn", UnitRole.Hero, 100)));
    }

    [TestMethod]
    public void Effective_DestroyedTower_IsZero()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);

        Assert.AreEqual(0, rules.Effective(Hit(FordsTestLayout.EastOuter0, UnitRole.Infantry, 100)));
    }

    [TestMethod]
    public void Effective_HitFromTheTowersOwnSide_IsZero()
    {
        var (rules, _) = Setup();

        var hit = new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 200, MatchSide.East);

        Assert.AreEqual(0, rules.Effective(hit));
    }

    [TestMethod]
    public void Effective_HitFromTheOtherSide_IsScaledAsBefore()
    {
        var (rules, _) = Setup();

        var hit = new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 200, MatchSide.West);

        Assert.AreEqual(30, rules.Effective(hit));
    }

    [TestMethod]
    public void Effective_HitWithNoKnownSide_IsScaledAsBefore()
    {
        var (rules, _) = Setup();

        var hit = new TowerHit(FordsTestLayout.EastOuter0, true, UnitRole.Ranged, 200, null);

        Assert.AreEqual(30, rules.Effective(hit));
    }

    [TestMethod]
    [DataRow("raw", "RawZero")]
    [DataRow("unknown", "UnknownTower")]
    [DataRow("destroyed", "Destroyed")]
    [DataRow("locked", "Locked")]
    [DataRow("own", "OwnSide")]
    [DataRow("applied", "Applied")]
    public void Decide_ReturnsTheReasonOfEachBranch(string scenario, string expectedName)
    {
        var expected = (TowerDamageReason)Enum.Parse(typeof(TowerDamageReason), expectedName);
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);
        var hit = scenario switch
        {
            "raw" => new TowerHit(FordsTestLayout.EastInner0, false, UnitRole.Infantry, 0),
            "unknown" => new TowerHit("barricade_17", false, UnitRole.Infantry, 10),
            "destroyed" => new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 10),
            "locked" => new TowerHit(FordsTestLayout.EastKeep, false, UnitRole.Infantry, 10),
            "own" => new TowerHit(FordsTestLayout.EastInner0, false, UnitRole.Infantry, 10, MatchSide.East),
            _ => new TowerHit(FordsTestLayout.EastInner0, false, UnitRole.Infantry, 10, MatchSide.West),
        };

        var damage = rules.Decide(hit, out var reason);

        Assert.AreEqual(expected, reason);
        Assert.AreEqual(expected == TowerDamageReason.Applied ? 10 : 0, damage);
    }

    [TestMethod]
    public void Effective_EqualsDecide_ForEveryExistingCase()
    {
        var (rules, chain) = Setup();
        chain.OnDestroyed(FordsTestLayout.EastOuter0);
        var hits = new[]
        {
            new TowerHit(FordsTestLayout.EastInner0, false, UnitRole.Infantry, 0),
            new TowerHit("barricade_17", false, UnitRole.Infantry, 10),
            new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 10),
            new TowerHit(FordsTestLayout.EastKeep, true, UnitRole.Hero, 10),
            new TowerHit(FordsTestLayout.EastInner0, true, UnitRole.Hero, 77, MatchSide.East),
            new TowerHit(FordsTestLayout.EastInner0, true, UnitRole.Siege, 77, MatchSide.West),
            new TowerHit(FordsTestLayout.EastInner0, false, UnitRole.Infantry, 3),
        };

        foreach (var hit in hits)
            Assert.AreEqual(rules.Decide(hit, out _), rules.Effective(hit));
    }
}
