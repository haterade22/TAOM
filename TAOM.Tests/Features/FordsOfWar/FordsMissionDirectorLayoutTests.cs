using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>What the director does with the layout it was given: the checks that follow the tower build, and how long the layout took.</summary>
[TestClass]
public class FordsMissionDirectorLayoutTests
{
    private const string EastOuterLane0 = "tower_east_0_0";
    private const string EastInnerLane0 = "tower_east_0_1";

    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void ClearThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void Start_EveryTowerOfALaneFailsToBuild_RefusesWithOneErrorNamingTheLaneAndTheTowers()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == EastOuterLane0 || s.Id == EastInnerLane0), Arg.Any<TowerPrefab>(), Arg.Any<int>())
            .Returns(false);

        Assert.IsFalse(f.Director.Start());

        f.Logger.Received(1).LogError(Arg.Is<string>(m =>
            m.Contains("refused") && m.Contains("lane 0 has no tower left for East") && m.Contains(EastOuterLane0) && m.Contains(EastInnerLane0)));
        Assert.IsFalse(f.Session.IsActive);
        f.Battlefield.DidNotReceive().CompleteDeployment();
        f.Towers.DidNotReceive().SetDestroyedHandler(Arg.Any<System.Action<string>>());
    }

    [TestMethod]
    public void Start_OnlyTheOuterTowerOfALaneFailsToBuild_StillStarts()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == EastOuterLane0), Arg.Any<TowerPrefab>(), Arg.Any<int>()).Returns(false);

        Assert.IsTrue(f.Director.Start());

        f.Logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Start_LogsHowLongThePlanAndTheTowersTook_OnOneLayoutLine()
    {
        var f = FordsDirectorFixture.Create();

        Assert.IsTrue(f.Director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => Regex.IsMatch(m, @"^\[FordsOfWar\]\[Layout\] timing plan=\d+\.\d ms towers=\d+\.\d ms$")));
    }

    [TestMethod]
    public void Start_AnEmptiedLaneRefusesTheStart_StillLogsTheTiming()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == EastOuterLane0 || s.Id == EastInnerLane0), Arg.Any<TowerPrefab>(), Arg.Any<int>())
            .Returns(false);

        Assert.IsFalse(f.Director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[FordsOfWar][Layout] timing plan=")));
    }
}
