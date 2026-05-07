using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.SmartCavalryAI;

namespace TAOM.Tests.Features.SmartCavalryAI;

[TestClass]
public class CavalryPathPlannerTests
{
    private CavalryPathPlanner _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new CavalryPathPlanner();

    private static IFormationAdapter MakeFormation(
        Vec2 position,
        float width = 10f,
        int countOfUnits = 30,
        bool isCavalry = false)
    {
        var f = Substitute.For<IFormationAdapter>();
        f.CurrentPosition.Returns(position);
        f.Width.Returns(width);
        f.CountOfUnits.Returns(countOfUnits);
        f.RepresentativeIsCavalry.Returns(isCavalry);
        f.FormationKey.Returns(new object());
        return f;
    }

    // -------- No reroute required --------

    [TestMethod]
    public void TryGetReroutePoint_NoFriendlyFormations_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter>(), out var waypoint);

        Assert.IsFalse(result);
        Assert.AreEqual(Vec2.Zero, waypoint);
    }

    [TestMethod]
    public void TryGetReroutePoint_OnlyFriendlyCavalryOnLine_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var friendlyCav = MakeFormation(new Vec2(50f, 0f), isCavalry: true);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { friendlyCav }, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryGetReroutePoint_FriendlyInfantryBehindCavalry_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var blocker = MakeFormation(new Vec2(-10f, 0f), width: 10f);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryGetReroutePoint_FriendlyInfantryPastTarget_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        // projDir = 105, length - 2 = 98; 105 > 98 → past target → skip
        var pastTarget = MakeFormation(new Vec2(105f, 0f), width: 10f);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { pastTarget }, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryGetReroutePoint_FriendlyInfantryOffLineLaterally_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        // Width=10 → halfWidth=5 → buffer=5+4=9. Lateral 20 > 9 → outside charge corridor.
        var offLine = MakeFormation(new Vec2(50f, 20f), width: 10f);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { offLine }, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryGetReroutePoint_TargetTooClose_ReturnsFalse()
    {
        var cav = new Vec2(50f, 50f);
        var target = new Vec2(50.5f, 50f);  // length < 1
        var blocker = MakeFormation(new Vec2(50.25f, 50f));

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryGetReroutePoint_EmptyFormationFiltered_ReturnsFalse()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var emptyFormation = MakeFormation(new Vec2(50f, 0f), countOfUnits: 0);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { emptyFormation }, out _);

        Assert.IsFalse(result);
    }

    // -------- Reroute path required --------

    [TestMethod]
    public void TryGetReroutePoint_FriendlyInfantryOnLine_ReturnsTrueAndProducesWaypoint()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var blocker = MakeFormation(new Vec2(50f, 0f), width: 10f);

        var result = _sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out var waypoint);

        Assert.IsTrue(result);
        Assert.AreNotEqual(Vec2.Zero, waypoint);
        // Waypoint should be near the blocker's X (~50) plus 4 for advance, with a non-zero lateral offset
        Assert.AreEqual(54f, waypoint.x, 0.5f);
        Assert.AreNotEqual(0f, waypoint.y, "Waypoint must offset laterally from charge line");
    }

    [TestMethod]
    public void TryGetReroutePoint_BlockerToNorth_SidestepsSouth()
    {
        // Charge direction = +X (east). Right vector = (0, -1) (south).
        // Blocker at (50, +5) — north of charge line.
        // num5 (projRight) = +5 · (0,-1) = -5. Sign = +1 (since num5 ≤ 0).
        // Sidestep = right * +1 = south. Waypoint y should be < 0.
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var blocker = MakeFormation(new Vec2(50f, 5f), width: 6f);

        Assert.IsTrue(_sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out var waypoint));

        Assert.IsTrue(waypoint.y < 0f, $"Expected y < 0 (south), got y={waypoint.y}");
    }

    [TestMethod]
    public void TryGetReroutePoint_BlockerToSouth_SidestepsNorth()
    {
        // Blocker at (50, -5). projRight = -5 · (0,-1) = +5. Sign = -1.
        // Sidestep = right * -1 = north. Waypoint y should be > 0.
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var blocker = MakeFormation(new Vec2(50f, -5f), width: 6f);

        Assert.IsTrue(_sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out var waypoint));

        Assert.IsTrue(waypoint.y > 0f, $"Expected y > 0 (north), got y={waypoint.y}");
    }

    [TestMethod]
    public void TryGetReroutePoint_MultipleBlockers_PicksClosest()
    {
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 0f);
        var farBlocker = MakeFormation(new Vec2(80f, 0f), width: 6f);
        var closeBlocker = MakeFormation(new Vec2(30f, 0f), width: 6f);

        Assert.IsTrue(_sut.TryGetReroutePoint(
            cav, target,
            new List<IFormationAdapter> { farBlocker, closeBlocker },
            out var waypoint));

        // Waypoint should be near the closer blocker's X (30) + advance 4 = 34
        Assert.AreEqual(34f, waypoint.x, 1f);
    }

    [TestMethod]
    public void TryGetReroutePoint_DiagonalCharge_ProducesSensibleWaypoint()
    {
        // Charge direction = (1,1)/sqrt(2). Blocker on the line.
        var cav = new Vec2(0f, 0f);
        var target = new Vec2(100f, 100f);
        var blocker = MakeFormation(new Vec2(50f, 50f), width: 6f);

        Assert.IsTrue(_sut.TryGetReroutePoint(cav, target, new List<IFormationAdapter> { blocker }, out var waypoint));
        Assert.AreNotEqual(Vec2.Zero, waypoint);
    }
}
