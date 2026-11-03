using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The director's side of the camps: creatures and ring at the start, their orders from the match, and their deaths.</summary>
[TestClass]
public class FordsMissionDirectorCampTests
{
    private static readonly Vec3f CampAt = new(500f, 25f, 0f);

    /// <summary>A started director on a map with one camp of two creatures.</summary>
    private static FordsDirectorFixture Started(int count = 2)
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>() with
        {
            Camps = new List<CampSlot> { new("camp_0", CampAt, "hill_troll", count, 250) },
        }, 0, 0, 0));
        Assert.IsTrue(f.Director.Start());
        return f;
    }

    private static void ReportsCampRemoval(FordsDirectorFixture f, string campId)
    {
        string camp;
        AgentKey? killer;
        f.Camps.TryRemove(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), out camp, out killer)
            .Returns(call =>
            {
                call[3] = campId;
                call[4] = (AgentKey?)null;
                return true;
            });
    }

    [TestMethod]
    public void Start_ACampOnTheMap_MakesTheTeamTheCreaturesAndTheRing()
    {
        var f = Started();

        f.Camps.Received(1).Prepare();
        f.Camps.Received(2).Spawn("camp_0", "hill_troll", Arg.Any<Vec3f>());
        f.Camps.Received(1).AddRing("camp_0", CampAt, CampRules.Radius);
        f.Camps.Received(1).SetAlerted("camp_0", CampAt, false);
    }

    [TestMethod]
    public void Start_NoCampOnTheMap_MakesNoTeam()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>() with { Camps = new List<CampSlot>() }, 0, 0, 0));

        Assert.IsTrue(f.Director.Start());

        f.Camps.DidNotReceive().Prepare();
    }

    [TestMethod]
    public void Start_NoCreatureCanBeMade_LeavesNoRingAndAClearedCamp()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>() with
        {
            Camps = new List<CampSlot> { new("camp_0", CampAt, "hill_troll", 2, 250) },
            CapturePoints = new List<CapturePointSlot>(),
        }, 0, 0, 0));
        f.Camps.Spawn(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Vec3f>()).Returns(false);

        Assert.IsTrue(f.Director.Start());

        f.Camps.DidNotReceive().AddRing(Arg.Any<string>(), Arg.Any<Vec3f>(), Arg.Any<float>());
        f.Logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("[Camp] camp_0: 0 of 2")));
    }

    [TestMethod]
    public void Start_TheNeutralTeamCannotBeMade_StillStartsWithoutCreatures()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>() with
        {
            Camps = new List<CampSlot> { new("camp_0", CampAt, "hill_troll", 2, 250) },
        }, 0, 0, 0));
        f.Camps.Prepare().Returns(false);

        Assert.IsTrue(f.Director.Start());

        f.Camps.DidNotReceive().Spawn(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Vec3f>());
    }

    [TestMethod]
    public void OnAgentRemoved_ACampCreature_IsCountedAgainstTheCampAndNotAsAUnit()
    {
        var f = Started(count: 1);
        ReportsCampRemoval(f, "camp_0");

        f.Director.OnAgentRemoved(new object(), null, true);

        f.Logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("[Camp] camp_0 cleared by nobody")));
        f.Battlefield.DidNotReceive().TryRemoveUnit(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), out Arg.Any<AgentKey>(), out Arg.Any<AgentKey?>());
    }

    [TestMethod]
    public void Tick_AUnitNearTheCamp_AlertsTheCreatures()
    {
        var f = Started();
        f.Sample(1, MatchSide.West, 0, new Vec3f(CampAt.X, CampAt.Y + 5f, 0f));
        f.Camps.ClearReceivedCalls();

        f.Director.Tick(31f);

        f.Camps.Received(1).SetAlerted("camp_0", CampAt, true);
    }

    [TestMethod]
    public void Shutdown_ClearsTheCamps()
    {
        var f = Started();

        f.Director.Shutdown();

        f.Camps.Received().Clear();
    }
}
