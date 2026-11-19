using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The side rule the spider and warg bites share (#594): a ridden mount is its rider's side, a riderless mount of
/// unknown side is spared, and everything else compares the agent's own side.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureBiteSidesTests
{
    private static IAgentAdapter LooseMount(bool knownSide)
    {
        var mount = Substitute.For<IAgentAdapter>();
        mount.IsMount.Returns(true);
        mount.RiderAgent.Returns((IAgentAdapter)null);
        mount.HasKnownSide.Returns(knownSide);
        return mount;
    }

    private static IAgentAdapter Soldier()
    {
        var soldier = Substitute.For<IAgentAdapter>();
        soldier.IsMount.Returns(false);
        soldier.HasKnownSide.Returns(false);   // the unknown-side rule is for mounts only
        return soldier;
    }

    [TestMethod]
    public void Spares_LooseMountOfUnknownSide_ReturnsTrue()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var mount = LooseMount(knownSide: false);
        biter.IsSameTeam(mount).Returns(false);
        Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
    }

    [TestMethod]
    public void Spares_LooseMountOfUnknownSideAndNoBiterSide_ReturnsTrue()
        => Assert.IsTrue(CreatureBiteSides.Spares(null, LooseMount(knownSide: false)));

    [TestMethod]
    public void Spares_LooseMountOnTheBitersSide_ReturnsTrue()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var mount = LooseMount(knownSide: true);
        biter.IsSameTeam(mount).Returns(true);
        Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
    }

    [TestMethod]
    public void Spares_LooseMountOnTheOtherSide_ReturnsFalse()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var mount = LooseMount(knownSide: true);
        biter.IsSameTeam(mount).Returns(false);
        Assert.IsFalse(CreatureBiteSides.Spares(biter, mount));
    }

    [TestMethod]
    public void Spares_LooseMountOfKnownSideAndNoBiterSide_ReturnsFalse()
        => Assert.IsFalse(CreatureBiteSides.Spares(null, LooseMount(knownSide: true)));

    [TestMethod]
    public void Spares_RiddenMount_TakesItsRidersSide()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var mount = LooseMount(knownSide: false);
        mount.RiderAgent.Returns(rider);
        biter.IsSameTeam(rider).Returns(true);
        biter.IsSameTeam(mount).Returns(false);
        Assert.IsTrue(CreatureBiteSides.Spares(biter, mount));
    }

    [TestMethod]
    public void Spares_RiddenMountOfAnEnemyRider_ReturnsFalse()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var rider = Substitute.For<IAgentAdapter>();
        var mount = LooseMount(knownSide: true);
        mount.RiderAgent.Returns(rider);
        biter.IsSameTeam(rider).Returns(false);
        biter.IsSameTeam(mount).Returns(true);   // the mount's own side must not be consulted
        Assert.IsFalse(CreatureBiteSides.Spares(biter, mount));
    }

    [TestMethod]
    public void Spares_SoldierOnFootOfTheBitersSide_ReturnsTrue()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var soldier = Soldier();
        biter.IsSameTeam(soldier).Returns(true);
        Assert.IsTrue(CreatureBiteSides.Spares(biter, soldier));
    }

    [TestMethod]
    public void Spares_SoldierOnFootOfTheOtherSide_ReturnsFalse()
    {
        var biter = Substitute.For<IAgentAdapter>();
        var soldier = Soldier();
        biter.IsSameTeam(soldier).Returns(false);
        Assert.IsFalse(CreatureBiteSides.Spares(biter, soldier));
    }
}
