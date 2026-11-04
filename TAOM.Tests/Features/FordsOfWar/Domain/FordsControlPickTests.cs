using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Which unit the pointer names when the commander takes control: the nearest in reach, and a hero in reach before any soldier.</summary>
[TestClass]
public class FordsControlPickTests
{
    private const float Reach = 80f;

    private static FordsControlCandidate Soldier(float x, float y) => new(x, y, false);

    private static FordsControlCandidate Hero(float x, float y) => new(x, y, true);

    [TestMethod]
    public void Nearest_NoUnits_NamesNone()
    {
        Assert.AreEqual(-1, FordsControlPick.Nearest(new FordsControlCandidate[0], 0f, 0f, Reach));
    }

    [TestMethod]
    public void Nearest_EveryUnitOutOfReach_NamesNone()
    {
        Assert.AreEqual(-1, FordsControlPick.Nearest(new[] { Soldier(81f, 0f), Hero(0f, 200f) }, 0f, 0f, Reach));
    }

    [TestMethod]
    public void Nearest_AUnitAtTheEdgeOfReach_IsInReach()
    {
        Assert.AreEqual(0, FordsControlPick.Nearest(new[] { Soldier(80f, 0f) }, 0f, 0f, Reach));
    }

    [TestMethod]
    public void Nearest_SeveralSoldiers_NamesTheNearestToThePointer()
    {
        var units = new[] { Soldier(150f, 100f), Soldier(104f, 103f), Soldier(90f, 90f) };

        Assert.AreEqual(1, FordsControlPick.Nearest(units, 100f, 100f, Reach));
    }

    [TestMethod]
    public void Nearest_AHeroInReach_WinsOverACloserSoldier_WhicheverComesFirst()
    {
        Assert.AreEqual(1, FordsControlPick.Nearest(new[] { Soldier(1f, 0f), Hero(70f, 0f) }, 0f, 0f, Reach));
        Assert.AreEqual(0, FordsControlPick.Nearest(new[] { Hero(70f, 0f), Soldier(1f, 0f) }, 0f, 0f, Reach));
    }

    [TestMethod]
    public void Nearest_TwoHeroesInReach_NamesTheNearer()
    {
        Assert.AreEqual(1, FordsControlPick.Nearest(new[] { Hero(70f, 0f), Hero(30f, 0f) }, 0f, 0f, Reach));
    }

    [TestMethod]
    public void Nearest_ANaNPixel_IsOutOfReach_AndBlocksNoSoldierInReach()
    {
        Assert.AreEqual(1, FordsControlPick.Nearest(new[] { Soldier(float.NaN, 0f), Soldier(40f, 0f) }, 0f, 0f, Reach));
        Assert.AreEqual(-1, FordsControlPick.Nearest(new[] { Hero(float.NaN, float.NaN) }, 0f, 0f, Reach));
    }

    [DataTestMethod]
    [DataRow(null, true)]
    [DataRow("horse", true)]
    [DataRow("horse_2", true)]
    [DataRow("camel", true)]
    [DataRow("mule", true)]
    [DataRow("warg", false)]
    [DataRow("spider", false)]
    [DataRow("elephant", false)]
    [DataRow("mumakil", false)]
    public void MayRide_OnlyOnFootOrAPlainMount_BecauseABehaviorTreeDrivesACreature(string? mount, bool allowed)
    {
        Assert.AreEqual(allowed, FordsControlPick.MayRide(mount));
    }

    [TestMethod]
    public void Nearest_AHeroOutOfReach_LosesToASoldierInReach()
    {
        Assert.AreEqual(1, FordsControlPick.Nearest(new[] { Hero(300f, 0f), Soldier(40f, 0f) }, 0f, 0f, Reach));
    }
}
