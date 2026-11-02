using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsTowerLinesTests
{
    [TestMethod]
    public void Attached_NamesTheTowerEntityChildrenAndHitPoints()
    {
        Assert.AreEqual("attached id=tower_west_1_0 entity=dwarf_tower_a children=13 hp=8000",
            FordsTowerLines.Attached("tower_west_1_0", "dwarf_tower_a", 13, 8000));
    }

    [TestMethod]
    public void Order_PrintsTheAttackerSideLanePositionFaceAndPath()
    {
        Assert.AreEqual("order id=tower_west_1_0 side=East lane=1 pos=(530.0,500.0,16.6) face=4 pathExists=True",
            FordsTowerLines.Order("tower_west_1_0", MatchSide.East, 1, new Vec3f(530f, 500f, 16.56f), 4, true));
    }

    [TestMethod]
    public void Order_NoFace_SaysNone()
    {
        StringAssert.Contains(FordsTowerLines.Order("t", MatchSide.West, 0, new Vec3f(1f, 2f, 3f), -1, false), "face=none pathExists=False");
    }

    [TestMethod]
    public void Fell_ReadsBackVisibilityAndPhysicsOfRootAndChild()
    {
        Assert.AreEqual("fell id=tower_west_1_0 rootVisible=False childVisible=False",
            FordsTowerLines.Fell("tower_west_1_0", false, false));
    }
}
