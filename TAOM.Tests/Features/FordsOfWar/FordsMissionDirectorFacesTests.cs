using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The face census is taken only on a map whose layout the scene itself authored, once per mission second.</summary>
[TestClass]
public class FordsMissionDirectorFacesTests
{
    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void ClearThreadGuard() => MissionThreadGuard.ResetForTests();

    private static FordsDirectorFixture Tagged(bool layoutReads)
    {
        var f = FordsDirectorFixture.Create(mode: MapLayoutMode.Tags, lanes: 2);
        var authored = FordsTestLayout.Create();
        MapLayout none;
        string problem;
        f.Scene.TryReadTaggedLayout(Arg.Any<string>(), Arg.Any<CampDefaults>(), out none, out problem)
            .Returns(call =>
            {
                call[2] = layoutReads ? authored : null;
                call[3] = layoutReads ? string.Empty : "fow_keep_east is missing";
                return layoutReads;
            });
        f.Battlefield.SampleFaces().Returns(new List<FordsFaceSample> { new(MatchSide.West, 0, 1, false, 0.1f, 1.2f) });
        Assert.IsTrue(f.Director.Start());
        return f;
    }

    private static void Second(FordsDirectorFixture f)
    {
        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);
    }

    [TestMethod]
    public void Tick_TagsMap_SamplesFacesOncePerMissionSecond()
    {
        var f = Tagged(layoutReads: true);

        Second(f);
        f.Director.Tick(0.1f);
        Assert.AreEqual(1, f.Battlefield.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IFordsBattlefieldAdapter.SampleFaces)));

        Second(f);
        Assert.AreEqual(2, f.Battlefield.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IFordsBattlefieldAdapter.SampleFaces)));
    }

    [TestMethod]
    public void Tick_TagsMap_HandsTheLookToTheReport()
    {
        var f = Tagged(layoutReads: true);

        Second(f);

        Assert.AreEqual(1, f.Report.Run!.Faces.Latest.Count);
    }

    [TestMethod]
    public void Tick_GeneratedMap_NeverSamplesFaces()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());

        Second(f);
        Second(f);

        f.Battlefield.DidNotReceive().SampleFaces();
    }

    [TestMethod]
    public void Tick_TagsMapThatFellBackToGenerated_NeverSamplesFaces()
    {
        var f = Tagged(layoutReads: false);

        Second(f);

        f.Battlefield.DidNotReceive().SampleFaces();
    }
}
