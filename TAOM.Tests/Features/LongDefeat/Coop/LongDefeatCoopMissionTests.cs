using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Coop;

[TestClass]
public class LongDefeatCoopMissionTests
{
    [TestCategory("RequiresGame")]   // constructs MissionOptionsComponent
    [TestMethod]
    public void Compose_PutsTheSessionBehavioursFirst()
    {
        var session = new MissionBehavior[] { new MissionOptionsComponent() };
        var own = new MissionBehavior[] { new MissionBoundaryPlacer(), new MissionAgentPanicHandler() };

        var all = LongDefeatCoopMission.Compose(session, own);

        CollectionAssert.AreEqual(session.Concat(own).ToArray(), all.ToArray());
    }

    [TestMethod]
    public void For_ASoloRun_HasNoSessionBehaviours_AndNoCoopSide()
    {
        var runs = new LongDefeatCoopRuns(new LongDefeatCoopState(), _ => true, new FakeLongDefeatMenu(), Substitute.For<IModLogger>());

        var parts = LongDefeatCoopMission.For(null, runs);

        Assert.AreEqual(0, parts.Session.Count);
        Assert.IsNull(parts.Run);
    }

    [TestCategory("RequiresGame")]   // constructs MissionOptionsComponent
    [TestMethod]
    public void For_ACoopRun_TakesCoopsBehaviours_AndHasACoopSide()
    {
        var runs = new LongDefeatCoopRuns(new LongDefeatCoopState(), _ => true, new FakeLongDefeatMenu(), Substitute.For<IModLogger>());
        var link = Substitute.For<ILongDefeatCoopLink>();
        link.CreateMissionBehaviors("s1").Returns(new MissionBehavior[] { new MissionOptionsComponent() });
        runs.Attach(link);
        runs.OnMissionOpenRequested("s1", LongDefeatSessionPayload.Encode("helms", RunOptions.Default));

        var parts = LongDefeatCoopMission.For("s1", runs);

        Assert.AreEqual(1, parts.Session.Count);
        Assert.IsNotNull(parts.Run);
    }

    [TestMethod]
    public void RunCoop_ReadsTheRoleOfItsSession_AndReportsTheEndThroughTheRuns()
    {
        var state = new LongDefeatCoopState();
        var menu = new FakeLongDefeatMenu();
        var runs = new LongDefeatCoopRuns(state, _ => true, menu, Substitute.For<IModLogger>());
        var link = Substitute.For<ILongDefeatCoopLink>();
        runs.Attach(link);
        runs.OnMissionOpenRequested("s1", LongDefeatSessionPayload.Encode("helms", RunOptions.Default));
        var hosted = 0;

        var coop = LongDefeatCoopMission.RunCoop("s1", runs, () => hosted++);
        Assert.AreEqual(LongDefeatCoopRole.Pending, coop.Role());

        runs.OnHostChanged("s1", 1, isLocalHost: true);
        coop.BecameHost();
        coop.Ended(new RunResult(RunEndReason.KeepLost, 3, 2, 40));

        Assert.AreEqual(LongDefeatCoopRole.Host, coop.Role());
        Assert.AreEqual(1, hosted);
        link.Received(1).SendRunEnded("s1", 1, (int)RunEndReason.KeepLost, 3, 2, 40);

        coop.MissionEnded();
        link.Received(1).Close();
        Assert.AreEqual(LongDefeatCoopRole.None, coop.Role());
    }
}
