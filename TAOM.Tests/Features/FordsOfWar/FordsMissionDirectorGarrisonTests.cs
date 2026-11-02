using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>On a real-tower map a tower's garrison stands on the scene's spots; a tower or keep with no spots has none.</summary>
[TestClass]
public class FordsMissionDirectorGarrisonTests
{
    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void ClearThreadGuard() => MissionThreadGuard.ResetForTests();

    private static FordsDirectorFixture Started(string towerWithSpots)
    {
        var f = FordsDirectorFixture.Create(mode: MapLayoutMode.Tags, lanes: 2, towerHalfWidth: 5.5f);
        var layout = FordsTestLayout.Create();
        var spots = new[] { new Vec3f(1f, 2f, 40f), new Vec3f(3f, 4f, 40f), new Vec3f(5f, 6f, 40f) };
        var authored = layout with { Towers = layout.Towers.Select(t => t.Id == towerWithSpots ? t with { GunnerSpots = spots } : t).ToList() };
        MapLayout none;
        string problem;
        f.Scene.TryReadTaggedLayout(Arg.Any<string>(), Arg.Any<CampDefaults>(), out none, out problem)
            .Returns(call =>
            {
                call[2] = authored;
                call[3] = string.Empty;
                return true;
            });
        Assert.IsTrue(f.Director.Start());
        return f;
    }

    [TestMethod]
    public void Start_TowerWithSpots_SpawnsOneBowmanPerSpot_AndNoOtherGarrison()
    {
        var f = Started(FordsTestLayout.EastOuter0);

        f.Towers.Received(1).SpawnGunners(Arg.Is<TowerSlot>(t => t.Id == FordsTestLayout.EastOuter0), Arg.Any<string>(), 3);
        f.Towers.Received(1).SpawnGunners(Arg.Any<TowerSlot>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Tick_RealTowerMap_LooksAtTheGarrisonEveryThirtySeconds()
    {
        var f = Started(FordsTestLayout.EastOuter0);

        for (var i = 0; i < 61; i++)
        {
            f.Report.MissionTick(1f, 1.0);
            f.Director.Tick(1f);
        }

        f.Towers.Received(3).LogGarrison();
    }

    [TestMethod]
    public void Tick_AnyMap_RunsTheBattlefieldHousekeepingEveryThreeSeconds()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());

        for (var i = 0; i < 7; i++)
            f.Director.Tick(1f);

        f.Battlefield.Received(2).Housekeep();
    }

    [TestMethod]
    public void Start_MapDeclaringLaneBars_PassesThemToTheBattlefield()
    {
        var bars = new LaneFaceBars(new[] { 1, 2 });
        var f = FordsDirectorFixture.Create(lanes: 2, laneBars: bars);

        Assert.IsTrue(f.Director.Start());

        f.Battlefield.Received(1).SetLaneBars(bars);
    }

    [TestMethod]
    public void Start_LaneBarsForAnotherLaneCount_PassesNone()
    {
        var f = FordsDirectorFixture.Create(lanes: 2, laneBars: new LaneFaceBars(new[] { 1, 2, 3 }));

        Assert.IsTrue(f.Director.Start());

        f.Battlefield.Received(1).SetLaneBars(null);
    }
}
