using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A hero of the match strikes harder and takes less from each blow, on both sides; every other troop is untouched.</summary>
[TestClass]
public class FordsHeroCombatTests
{
    private static HeroCombatFactors Factors(float damage = 3f, float taken = 0.4f) =>
        HeroCombatFactors.Of(Factions().Values.SelectMany(f => f.Heroes), FordsMatchConfig.Default.Heroes with { DamageFactor = damage, DamageTakenFactor = taken });

    private static string HeroOf(MatchSide side) => Factions()[side].Heroes[0].HeroId;

    [TestMethod]
    public void AHeroOfEitherSide_GetsBothFactors()
    {
        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            Assert.AreEqual(3f, Factors().Damage(HeroOf(side)), side.ToString());
            Assert.AreEqual(0.4f, Factors().Taken(HeroOf(side)), side.ToString());
        }
    }

    [TestMethod]
    public void AnyOtherTroop_GetsOne()
    {
        var soldier = Factions()[MatchSide.West].Units[0].TroopId;

        Assert.AreEqual(1f, Factors().Damage(soldier));
        Assert.AreEqual(1f, Factors().Taken(soldier));
    }

    [TestMethod]
    public void NoCharacter_GetsOne()
    {
        Assert.AreEqual(1f, Factors().Damage(null));
        Assert.AreEqual(1f, Factors().Taken(null));
    }

    [TestMethod]
    public void TheDefaultFactors_ReachTheHeroes()
    {
        var factors = HeroCombatFactors.Of(Factions().Values.SelectMany(f => f.Heroes), FordsMatchConfig.Default.Heroes);

        Assert.AreEqual(FordsMatchConfig.Default.Heroes.DamageFactor, factors.Damage(HeroOf(MatchSide.East)));
        Assert.AreEqual(FordsMatchConfig.Default.Heroes.DamageTakenFactor, factors.Taken(HeroOf(MatchSide.East)));
    }
}
