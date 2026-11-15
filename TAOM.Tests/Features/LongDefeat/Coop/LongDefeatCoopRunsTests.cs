using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Coop;

[TestClass]
public class LongDefeatCoopRunsTests
{
    private const string Session = "session-1";

    private readonly FakeLongDefeatMenu _menu = new();
    private readonly List<LongDefeatSetup> _started = new();
    private readonly FakeLink _link = new();
    private LongDefeatCoopState _state = null!;
    private LongDefeatCoopRuns _runs = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new LongDefeatCoopState();
        _runs = new LongDefeatCoopRuns(_state, setup => { _started.Add(setup); return true; }, _menu, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void WithNoLink_CoopIsNotAvailable_AndHostStartsNothing()
    {
        Assert.IsFalse(_state.IsAvailable);

        _runs.Host(new LongDefeatSetup("helms", "menu", RunOptions.Default));

        Assert.AreEqual(0, _link.Hosted.Count);
        Assert.AreEqual(1, _menu.Messages.Count);
    }

    [TestMethod]
    public void Host_SendsTheSetupAsThePayload()
    {
        _runs.Attach(_link);

        _runs.Host(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "cavalry" }));

        Assert.IsTrue(LongDefeatSessionPayload.TryDecode(_link.Hosted.Single(), out var fortress, out var options));
        Assert.AreEqual("helms", fortress);
        Assert.AreEqual("cavalry", options!.ClassId);
    }

    [TestMethod]
    public void MissionOpen_StartsTheRunWithTheSession_AndTheRoleWaitsForTheHost()
    {
        _runs.Attach(_link);

        _runs.OnMissionOpenRequested(Session, LongDefeatSessionPayload.Encode("helms", RunOptions.Default));

        var setup = _started.Single();
        Assert.AreEqual("helms", setup.FortressId);
        Assert.AreEqual(Session, setup.SessionId);
        Assert.AreEqual(LongDefeatCoopRole.Pending, _state.RoleFor(Session));
    }

    [TestMethod]
    public void MissionOpen_WithABadPayload_StartsNothing_AndTellsThePlayer()
    {
        _runs.Attach(_link);

        _runs.OnMissionOpenRequested(Session, new byte[] { 99 });

        Assert.AreEqual(0, _started.Count);
        Assert.AreEqual(1, _menu.Messages.Count);
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    [TestMethod]
    public void HostChanged_SetsTheRole_ForThisSessionOnly()
    {
        Open();

        _runs.OnHostChanged("other-session", 1, isLocalHost: true);
        Assert.AreEqual(LongDefeatCoopRole.Pending, _state.RoleFor(Session));

        _runs.OnHostChanged(Session, 1, isLocalHost: false);
        Assert.AreEqual(LongDefeatCoopRole.Follower, _state.RoleFor(Session));
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor("other-session"));
    }

    [TestMethod]
    public void HostChanged_FromAnOlderEpoch_IsIgnored()
    {
        Open();
        _runs.OnHostChanged(Session, 2, isLocalHost: true);

        _runs.OnHostChanged(Session, 1, isLocalHost: false);

        Assert.AreEqual(LongDefeatCoopRole.Host, _state.RoleFor(Session));
    }

    // Slice 1 has no director handoff: a follower made host ends the run for everyone.
    [TestMethod]
    public void AFollowerPromotedToHost_ClosesTheSession_AndTellsThePlayer()
    {
        Open();
        _runs.OnHostChanged(Session, 1, isLocalHost: false);

        _runs.OnHostChanged(Session, 2, isLocalHost: true);

        Assert.AreEqual(1, _link.Closes);
        Assert.AreEqual(1, _menu.Messages.Count);
        Assert.AreEqual(1, _menu.EndGames, "the promoted follower ends at once, it does not wait for Coop's end event");
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    [TestMethod]
    public void ReportEnded_OnTheHost_SendsTheResult()
    {
        Open();
        _runs.OnHostChanged(Session, 3, isLocalHost: true);
        var result = new RunResult(RunEndReason.DefendersDead, 5, 4, 120);

        _runs.ReportEnded(Session, result);

        Assert.AreEqual((Session, 3, (int)RunEndReason.DefendersDead, 5, 4, 120), _link.Ended.Single());
    }

    [TestMethod]
    public void ReportEnded_OnAFollower_SendsNothing()
    {
        Open();
        _runs.OnHostChanged(Session, 1, isLocalHost: false);

        _runs.ReportEnded(Session, new RunResult(RunEndReason.DefendersDead, 5, 4, 120));

        Assert.AreEqual(0, _link.Ended.Count);
    }

    [TestMethod]
    public void RunEnded_OnAFollower_TellsThePlayer_UnlessItIsFromAnotherSessionOrAnOlderHost()
    {
        Open();
        _runs.OnHostChanged(Session, 2, isLocalHost: false);

        _runs.OnRunEnded("other-session", 2, (int)RunEndReason.DefendersDead, 5, 4, 120);
        _runs.OnRunEnded(Session, 1, (int)RunEndReason.DefendersDead, 5, 4, 120);
        Assert.AreEqual(0, _menu.Messages.Count);

        _runs.OnRunEnded(Session, 2, (int)RunEndReason.DefendersDead, 5, 4, 120);
        Assert.AreEqual(1, _menu.Messages.Count);
        var values = _menu.MessageValues.Single();
        Assert.AreEqual("5|4|120", values["WAVE"] + "|" + values["WAVES"] + "|" + values["KILLS"]);
    }

    [TestMethod]
    public void OnMissionOpenRequested_WhenTheRunCannotStart_LeavesNoSession()
    {
        var runs = new LongDefeatCoopRuns(_state, _ => false, _menu, Substitute.For<IModLogger>());
        runs.Attach(_link);

        runs.OnMissionOpenRequested(Session, LongDefeatSessionPayload.Encode("helms", RunOptions.Default));

        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    // Every exit (the end panel, the escape menu, a retreat, a fault) ends the mission; its end is where the host closes the session.
    [TestMethod]
    public void MissionEnded_OnTheHost_ClosesTheSession_AndClearsTheState()
    {
        Open();
        _runs.OnHostChanged(Session, 1, isLocalHost: true);

        _runs.MissionEnded(Session);

        Assert.AreEqual(1, _link.Closes);
        Assert.AreEqual(0, _menu.EndGames, "the mission is already ending");
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    // Coop's own controller leaves the session for a member whose mission ends.
    [TestMethod]
    public void MissionEnded_OnAFollower_ClearsTheState_WithoutAClose()
    {
        Open();
        _runs.OnHostChanged(Session, 1, isLocalHost: false);

        _runs.MissionEnded(Session);

        Assert.AreEqual(0, _link.Closes);
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    // Coop's end event can arrive after the player's own exit; by then it must end no other game.
    [TestMethod]
    public void AnEndEvent_AfterTheMissionEnded_EndsNothing()
    {
        Open();
        _runs.OnHostChanged(Session, 1, isLocalHost: true);
        _runs.MissionEnded(Session);

        _runs.OnMissionEndRequested(Session);
        _runs.OnHostChanged(Session, 2, isLocalHost: true);

        Assert.AreEqual(0, _menu.EndGames);
        Assert.AreEqual(1, _link.Closes);
    }

    [TestMethod]
    public void MissionEnd_ForThisSession_EndsTheGameOnce()
    {
        Open();

        _runs.OnMissionEndRequested("other-session");
        _runs.OnMissionEndRequested(Session);
        _runs.OnMissionEndRequested(Session);

        Assert.AreEqual(1, _menu.EndGames);
        Assert.AreEqual(LongDefeatCoopRole.None, _state.RoleFor(Session));
    }

    [TestMethod]
    public void Join_ParsesTheAddress_AndRefusesABadOne()
    {
        _runs.Attach(_link);

        _runs.Join("not an address");
        Assert.AreEqual(0, _link.Joined.Count);
        Assert.AreEqual(1, _menu.Messages.Count);

        _runs.Join(" 192.168.1.20:4200 ");
        Assert.AreEqual(("192.168.1.20", 4200), _link.Joined.Single());
    }

    [TestMethod]
    public void SessionBehaviors_AskTheLinkOnlyForTheOpenSession()
    {
        Open();

        Assert.AreEqual(0, LongDefeatCoopMission.SessionBehaviors("other-session", _state).Count);
        Assert.AreEqual(1, LongDefeatCoopMission.SessionBehaviors(Session, _state).Count);
    }

    private void Open()
    {
        _runs.Attach(_link);
        _runs.OnMissionOpenRequested(Session, LongDefeatSessionPayload.Encode("helms", RunOptions.Default));
    }

    private sealed class FakeLink : ILongDefeatCoopLink
    {
        public List<byte[]> Hosted { get; } = new();
        public List<(string, int)> Joined { get; } = new();
        public List<(string, int, int, int, int, int)> Ended { get; } = new();
        public int Closes { get; private set; }
        public bool CloseResult { get; set; }

        public void Host(byte[] payload) => Hosted.Add(payload);

        public void Join(string address, int port) => Joined.Add((address, port));

        public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId) => new MissionBehavior[] { null! };

        public bool Close()
        {
            Closes++;
            return CloseResult;
        }

        public bool SendRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills)
        {
            Ended.Add((sessionId, epoch, reason, wave, wavesCleared, kills));
            return true;
        }
    }
}
