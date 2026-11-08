using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The director gives a lane formation its tactic's arrangement once per change, before the movement order.</summary>
[TestClass]
public class FordsTacticsDirectorTests
{
    private static readonly LaneTactic Wall = new(LaneArrangement.Line, LaneArrangement.ShieldWall, CloseRange: 45f, ChargeRange: 6f);

    private static FordsDirectorFixture Started(LaneTactic? west)
    {
        var f = FordsDirectorFixture.Create(factions: all => all.ToDictionary(
            p => p.Key, p => p.Key == MatchSide.West ? p.Value with { Tactic = west } : p.Value));
        Assert.IsTrue(f.Director.Start());
        f.Battlefield.Move(Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<Vec3f>()).Returns(true);
        f.Battlefield.SetArrangement(Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<LaneArrangement>()).Returns(true);
        return f;
    }

    private static IReadOnlyList<Vec3f> LanePath(FordsDirectorFixture f, int lane)
    {
        var layout = (MapLayout)f.Scene.ReceivedCalls().First(c => c.GetMethodInfo().Name == nameof(IFordsSceneAdapter.Fit)).GetArguments()[0]!;
        return layout.Lanes.Single(l => l.Index == lane).Path;
    }

    /// <summary>A West unit at the middle of lane 0 and an East unit the first path vertex more than 20 m further on.</summary>
    private static void Facing(FordsDirectorFixture f)
    {
        var path = LanePath(f, 0);
        var mid = path.Count / 2;
        var ahead = Enumerable.Range(mid + 1, path.Count - mid - 1).First(i => path[i].Distance2D(path[mid]) > 20f);
        Assert.IsTrue(path[ahead].Distance2D(path[mid]) < 45f, "the enemy stands inside the close range");
        f.Sample(1, MatchSide.West, 0, path[mid]);
        f.Sample(2, MatchSide.East, 0, path[ahead]);
    }

    [TestMethod]
    public void ALaneClosingOnAnEnemy_GetsItsCloseArrangementOnce_BeforeItsMove()
    {
        var f = Started(Wall);
        Facing(f);

        f.Director.Tick(1f);
        f.Director.Tick(1f);

        f.Battlefield.Received(1).SetArrangement(MatchSide.West, 0, LaneArrangement.ShieldWall);
        var calls = f.Battlefield.ReceivedCalls().Where(c => c.GetArguments() is { Length: > 1 } a
            && a[0] is MatchSide.West && a[1] is 0).Select(c => c.GetMethodInfo().Name).ToList();
        Assert.IsTrue(calls.IndexOf(nameof(IFordsBattlefieldAdapter.SetArrangement)) < calls.IndexOf(nameof(IFordsBattlefieldAdapter.Move)),
            string.Join(",", calls));
    }

    [TestMethod]
    public void AFactionWithoutATactic_IsNeverGivenAnArrangement()
    {
        var f = Started(null);
        Facing(f);

        f.Director.Tick(1f);

        f.Battlefield.DidNotReceive().SetArrangement(Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<LaneArrangement>());
    }

    [TestMethod]
    public void AnArrangementTheAdapterCouldNotGive_IsAskedAgainOnTheNextTick()
    {
        var f = Started(Wall);
        f.Battlefield.SetArrangement(MatchSide.West, 0, LaneArrangement.ShieldWall).Returns(false, true);
        Facing(f);

        f.Director.Tick(1f);
        f.Director.Tick(1f);
        f.Director.Tick(1f);

        f.Battlefield.Received(2).SetArrangement(MatchSide.West, 0, LaneArrangement.ShieldWall);
    }
}
