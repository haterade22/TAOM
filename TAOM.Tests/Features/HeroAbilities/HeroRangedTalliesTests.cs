using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.HeroAbilities.Hooks;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// One mission's ranged hero tallies (#1361): one per hero body, the kit's switches taken when it is made, and a missile
/// removal handed to every tally, because the removal names no shooter.
/// </summary>
[TestClass]
public class HeroRangedTalliesTests
{
    private const string Legolas = "taom_hero_legolas";

    private static readonly HeroRangedConfig Kit = new HeroRangedConfig { ZeroAimError = true, PierceShields = true };

    private sealed class Body
    {
        internal Body(string heroId = Legolas)
        {
            HeroId = heroId;
        }

        internal string HeroId { get; }
    }

    private static HeroRangedTallies<Body> New() => new HeroRangedTallies<Body>(b => b.HeroId);

    [TestMethod]
    public void Of_SameBody_IsOneTally()
    {
        var tallies = New();
        var body = new Body();

        var first = tallies.Of(body, Kit);

        Assert.AreSame(first, tallies.Of(body, Kit));
        Assert.AreEqual(1, tallies.All.Count());
    }

    [TestMethod]
    public void Of_ASecondBodyOfTheSameHero_TakesTheNextSerial()
    {
        var tallies = New();

        var first = tallies.Of(new Body(), Kit);
        var other = tallies.Of(new Body("taom_hero_gimli"), Kit);
        var second = tallies.Of(new Body(), Kit);

        Assert.AreEqual(1, first.Serial);
        Assert.AreEqual(1, other.Serial);
        Assert.AreEqual(2, second.Serial);
        Assert.AreEqual(Legolas, second.HeroId);
    }

    [TestMethod]
    public void Of_TakesTheKitSwitches_AndNoKitIsOff()
    {
        var tallies = New();

        var on = tallies.Of(new Body(), Kit);
        var aimOnly = tallies.Of(new Body(), new HeroRangedConfig { ZeroAimError = true });
        var none = tallies.Of(new Body(), null);

        Assert.IsTrue(on.ZeroAimError && on.PierceShields);
        Assert.IsTrue(aimOnly.ZeroAimError && !aimOnly.PierceShields);
        Assert.IsFalse(none.ZeroAimError || none.PierceShields);
    }

    [TestMethod]
    public void MissileRemoved_ReachesEveryTally()
    {
        var tallies = New();
        var a = tallies.Of(new Body(), Kit);
        var b = tallies.Of(new Body(), Kit);
        var victim = new object();
        a.MissileHit(victim, 7, false, false, false, false);
        b.MissileHit(victim, 7, false, false, false, false);

        tallies.MissileRemoved(7);
        a.MissileHit(victim, 7, false, false, false, false);
        b.MissileHit(victim, 7, false, false, false, false);

        Assert.AreEqual(2, a.Contact);
        Assert.AreEqual(2, b.Contact);
    }

    [TestMethod]
    public void Clear_DropsEveryTally()
    {
        var tallies = New();
        tallies.Of(new Body(), Kit);

        tallies.Clear();

        Assert.AreEqual(0, tallies.All.Count());
    }
}
