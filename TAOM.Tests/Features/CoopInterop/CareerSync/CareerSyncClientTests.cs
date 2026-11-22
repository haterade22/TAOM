using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.Tests.Features.CoopInterop.CareerSync;

// A co-op client's side of career sync: it reports its own hero's career to the server and writes what the server holds.
[TestClass]
public class CareerSyncClientTests
{
    private const string Own = "Player1";
    private CareerDataService _data = null!;
    private ICareerPassiveService _passives = null!;
    private ICareerRegistry _registry = null!;
    private ICoopSessionProvider _coop = null!;
    private ICareerSyncNotice _notice = null!;
    private CareerSyncClient _client = null!;
    private readonly List<(CareerSyncSnapshot Career, long Sequence, bool JoinReport)> _sent = new();

    private sealed class Link : ICareerSyncLink
    {
        private readonly List<(CareerSyncSnapshot, long, bool)> _sent;
        public Link(List<(CareerSyncSnapshot, long, bool)> sent) => _sent = sent;

        public bool SendToHost(CareerSyncSnapshot career, long sequence, bool joinReport)
        {
            _sent.Add((career, sequence, joinReport));
            return true;
        }
    }

    [TestInitialize]
    public void Setup()
    {
        _data = new CareerDataService();
        _passives = Substitute.For<ICareerPassiveService>();
        _registry = Substitute.For<ICareerRegistry>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsCoopClient.Returns(true);
        var player = Substitute.For<IPlayerPartyAdapter>();
        player.GetMainHeroId().Returns(Own);
        _notice = Substitute.For<ICareerSyncNotice>();
        _client = new CareerSyncClient(_data, _registry, _passives, _coop, player, _notice, Substitute.For<IModLogger>());
        _sent.Clear();
        _client.Attach(new Link(_sent));
    }

    private static CareerSyncSnapshot Career(string hero, string career) =>
        new(hero, career, new[] { career + "_root" }, Array.Empty<int>(), Array.Empty<string>());

    [TestMethod]
    public void AChange_BeforeTheJoinIsReady_SendsNothing()
    {
        _data.SetCareer(Own, "ranger");
        _client.Flush();

        Assert.AreEqual(0, _sent.Count);
    }

    [TestMethod]
    public void OnJoined_SendsAJoinReportOfTheOwnHero()
    {
        _data.SetCareer(Own, "ranger");

        _client.OnJoined();

        Assert.AreEqual(1, _sent.Count);
        Assert.IsTrue(_sent[0].JoinReport);
        Assert.AreEqual(Own, _sent[0].Career.HeroId);
        Assert.AreEqual("ranger", _sent[0].Career.CareerId);
    }

    [TestMethod]
    public void AFreeSwitch_ManyChangesInOneAction_SendsOnceAtTheNextFlush()
    {
        // CareerSwitchService and CareerCreationHandler: clear, set, stamp the tree flag, take the root.
        _client.OnJoined();
        _sent.Clear();

        _data.ClearCareer(Own);
        _data.SetCareer(Own, "warden");
        _data.SetFlag(Own, "career_tree_v2");
        _data.TryAddChoice(Own, "warden_root", 5);
        Assert.AreEqual(0, _sent.Count, "nothing goes out mid-action");

        _client.Flush();
        _client.Flush();

        Assert.AreEqual(1, _sent.Count);
        Assert.IsFalse(_sent[0].JoinReport);
        CollectionAssert.AreEqual(new[] { "warden_root" }, new List<string>(_sent[0].Career.Choices));
    }

    [TestMethod]
    public void AnotherHerosChange_IsNotSent()
    {
        _client.OnJoined();
        _sent.Clear();

        _data.SetCareer("lord_1_1", "ranger");
        _client.Flush();

        Assert.AreEqual(0, _sent.Count);
    }

    [TestMethod]
    public void NotACoopClient_SendsNothing()
    {
        _coop.IsCoopClient.Returns(false);
        _client.OnJoined();

        _data.SetCareer(Own, "ranger");
        _client.Flush();

        Assert.AreEqual(0, _sent.Count);
    }

    [TestMethod]
    public void NoLink_SendsNothing()
    {
        _client.Attach(null);
        _client.OnJoined();

        _data.SetCareer(Own, "ranger");
        _client.Flush();

        Assert.AreEqual(0, _sent.Count);
    }

    [TestMethod]
    public void Receive_WritesTheServersCopy_AndRefreshesPassives_WithoutSendingItBack()
    {
        _client.OnJoined();
        _sent.Clear();

        _client.Receive(Career("Player2", "ranger"), 0, refused: false, known: true);
        _client.Flush();

        Assert.AreEqual("ranger", _data.GetCareerStringId("Player2"));
        _passives.Received(1).RefreshCache(_data, _registry);
        Assert.AreEqual(0, _sent.Count);
    }

    [TestMethod]
    public void Receive_BeforeTheJoinIsReady_IsStillWritten()
    {
        // Coop queues messages for a peer that is still loading and replays them, so nothing is held back.
        _client.Receive(Career("Player2", "ranger"), 0, refused: false, known: true);

        Assert.AreEqual("ranger", _data.GetCareerStringId("Player2"));
    }

    [TestMethod]
    public void StaleEcho_ForTheOwnHero_IsIgnored()
    {
        _client.OnJoined();
        _data.SetCareer(Own, "warden");
        _client.Flush();
        var last = _sent[_sent.Count - 1].Sequence;

        _client.Receive(Career(Own, "ranger"), last - 1, refused: true, known: true);

        Assert.AreEqual("warden", _data.GetCareerStringId(Own), "an answer to an older report must not undo a newer one");
        _notice.DidNotReceive().ShowRefused();
    }

    [TestMethod]
    public void AnAnswerToTheLatestReport_IsWritten()
    {
        _client.OnJoined();
        _data.SetCareer(Own, "warden");
        _client.Flush();
        var last = _sent[_sent.Count - 1].Sequence;

        _client.Receive(Career(Own, "ranger"), last, refused: false, known: true);

        Assert.AreEqual("ranger", _data.GetCareerStringId(Own));
    }

    [TestMethod]
    public void AnAnswer_WhileANewerChangeWaits_IsIgnored()
    {
        _client.OnJoined();
        var last = _sent[_sent.Count - 1].Sequence;
        _data.SetCareer(Own, "warden");

        _client.Receive(Career(Own, "ranger"), last, refused: false, known: true);

        Assert.AreEqual("warden", _data.GetCareerStringId(Own), "the waiting report goes at the next flush and is answered itself");
    }

    [TestMethod]
    public void ARefusal_ShowsOneLine_AndWritesTheServersCopyWhenItHasOne()
    {
        _client.OnJoined();
        var last = _sent[_sent.Count - 1].Sequence;

        _client.Receive(Career(Own, "ranger"), last, refused: true, known: true);

        _notice.Received(1).ShowRefused();
        Assert.AreEqual("ranger", _data.GetCareerStringId(Own));
    }

    [TestMethod]
    public void ARefusal_WithoutAServerCopy_KeepsTheOwnCareer_AndSaysTheServerDoesNotSaveIt()
    {
        _data.SetCareer(Own, "warden");
        _client.OnJoined();
        var last = _sent[_sent.Count - 1].Sequence;

        _client.Receive(new CareerSyncSnapshot(Own, null, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<string>()), last, refused: true, known: false);

        // The server holds no career for this hero, so the refused one is lost at the next join: the player is told.
        _notice.Received(1).ShowRefusedUnsaved();
        _notice.DidNotReceive().ShowRefused();
        Assert.AreEqual("warden", _data.GetCareerStringId(Own), "kept on this PC: a career picked before career sync must not be wiped");
    }

    [TestMethod]
    public void AStaleRefusal_WithoutAServerCopy_ShowsNoLine()
    {
        _data.SetCareer(Own, "warden");
        _client.OnJoined();
        var last = _sent[_sent.Count - 1].Sequence;

        _client.Receive(new CareerSyncSnapshot(Own, null, Array.Empty<string>(), Array.Empty<int>(), Array.Empty<string>()), last - 1, refused: true, known: false);

        _notice.DidNotReceive().ShowRefusedUnsaved();
    }

    [TestMethod]
    public void Sequences_GrowWithEveryReport()
    {
        _client.OnJoined();
        _data.SetCareer(Own, "warden");
        _client.Flush();

        Assert.IsTrue(_sent[1].Sequence > _sent[0].Sequence);
    }
}
