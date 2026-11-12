using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.CoopCompat.Handshake;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using FingerprintReport = TAOM.Features.CoopInterop.SettingsFingerprint.FingerprintReport;

namespace TAOM.CoopCompat.Tests.Handshake;

[TestClass]
public class SettingsHandshakeServiceTests
{
    // MCM's group attribute is read by name, so a synthetic one stands in for it.
    private sealed class SettingPropertyGroupAttribute : Attribute
    {
        public SettingPropertyGroupAttribute(string groupName) => GroupName = groupName;
        public string GroupName { get; }
    }

    private sealed class FakeSettings
    {
        [SettingPropertyGroup("Battle Tactics")] public bool Charge { get; set; } = true;
        [SettingPropertyGroup("World")] public int Speed { get; set; } = 3;
    }

    private sealed class RecordingNotice : ISettingsNotice
    {
        public List<string[]> Differs { get; } = new();
        public int BuildMismatches { get; private set; }

        public void SettingsDiffer(IReadOnlyList<string> groups) => Differs.Add(groups.ToArray());

        public void BuildMismatch() => BuildMismatches++;
    }

    private FakeCoopTransport _transport = null!;
    private RecordingNotice _notice = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private FakeSettings _local = null!;
    private BuildIdentity _identity = null!;
    private SettingsHandshakeService _service = null!;

    private static BuildIdentity Identity(string taom = "build.1", string taomMvid = "m1", string compatMvid = "c1", int protocol = CoopCompatProtocol.ProtocolVersion)
        => new BuildIdentity(protocol, taom, taomMvid, compatMvid);

    private static FingerprintReport Unmeasured() => SettingsFingerprint.ComputeAcross(new object[] { null! });

    [TestInitialize]
    public void Setup()
    {
        _transport = new FakeCoopTransport();
        _notice = new RecordingNotice();
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _local = new FakeSettings();
        _identity = Identity();
        _service = new SettingsHandshakeService(_transport, () => SettingsFingerprint.ComputeAcross(_local), _identity, _notice, _logger, _outcomes);
    }

    private CoopCompatHello HelloFrom(FakeSettings settings, BuildIdentity? identity = null)
    {
        var id = identity ?? _identity;
        var report = SettingsFingerprint.ComputeAcross(settings);
        var names = report.ByGroup.Keys.ToArray();
        return new CoopCompatHello
        {
            ProtocolVersion = id.ProtocolVersion,
            TaomVersion = id.TaomVersion,
            TaomMvid = id.TaomMvid,
            CoopCompatMvid = id.CoopCompatMvid,
            SettingsGlobal = report.Global,
            GroupNames = names,
            GroupHashes = names.Select(n => report.ByGroup[n]).ToArray(),
            Conclusive = report.IsConclusive,
        };
    }

    private CoopCompatHelloReply ServerReplyTo(CoopCompatHello hello)
    {
        _transport.CurrentRole = CoopRole.Server;
        _transport.RepliesSent.Clear();
        _transport.RaiseHello(FakeCoopTransport.Peer("7"), hello);
        return _transport.RepliesSent.Single().Reply;
    }

    [TestMethod]
    public void ClientReady_OnAClient_SendsAHelloWithTheLocalFingerprint()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseClientReady();

        var hello = _transport.HellosSent.Single();
        var report = SettingsFingerprint.ComputeAcross(_local);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, hello.ProtocolVersion);
        Assert.AreEqual("build.1", hello.TaomVersion);
        Assert.AreEqual("m1", hello.TaomMvid);
        Assert.AreEqual("c1", hello.CoopCompatMvid);
        Assert.AreEqual(report.Global, hello.SettingsGlobal);
        CollectionAssert.AreEqual(report.ByGroup.Keys.ToArray(), hello.GroupNames);
        CollectionAssert.AreEqual(report.ByGroup.Values.ToArray(), hello.GroupHashes);
        Assert.IsTrue(hello.Conclusive);
    }

    [TestMethod]
    public void ClientReady_WhenNothingWasMeasured_SendsAnInconclusiveHello()
    {
        _transport = new FakeCoopTransport();
        _service = new SettingsHandshakeService(_transport, Unmeasured, _identity, _notice, _logger, _outcomes);
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseClientReady();

        Assert.IsFalse(_transport.HellosSent.Single().Conclusive);
    }

    [TestMethod]
    public void ClientReady_WhenTheSendFails_LogsAndRaisesNoNotice()
    {
        _transport.CurrentRole = CoopRole.Client;
        _transport.SendsSucceed = false;

        _transport.RaiseClientReady();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("[CoopCompat]")));
        Assert.AreEqual(0, _notice.Differs.Count);
    }

    [TestMethod]
    public void ClientReady_OnAServerOrWithoutASession_SendsNothing()
    {
        _transport.CurrentRole = CoopRole.Server;
        _transport.RaiseClientReady();
        _transport.CurrentRole = CoopRole.NoSession;
        _transport.RaiseClientReady();

        Assert.AreEqual(0, _transport.HellosSent.Count);
    }

    [TestMethod]
    public void Hello_WithMatchingSettings_RepliesMatch_AndLogsMatch()
    {
        var reply = ServerReplyTo(HelloFrom(new FakeSettings()));

        Assert.AreEqual((int)HandshakeVerdict.Match, reply.Verdict);
        Assert.AreEqual(0, reply.DivergentGroups.Length);
        Assert.IsFalse(reply.BuildMismatch);
        Assert.AreEqual(CoopCompatProtocol.ProtocolVersion, reply.ProtocolVersion);
        _logger.Received().LogInfo("[CoopCompat] settings peer=7 match");
        _outcomes.Received().Pass("taom.coopcompat.settings.server", "7", Arg.Is<string>(d => d.Contains("verdict=match")));
    }

    [TestMethod]
    public void Hello_WithADifferentGroup_RepliesDiffer_NamingOnlyThatGroup()
    {
        var reply = ServerReplyTo(HelloFrom(new FakeSettings { Charge = false }));

        Assert.AreEqual((int)HandshakeVerdict.Differ, reply.Verdict);
        CollectionAssert.AreEqual(new[] { "Battle Tactics" }, reply.DivergentGroups);
        _logger.Received().LogInfo("[CoopCompat] settings peer=7 differ: Battle Tactics");
        _outcomes.Received().Pass("taom.coopcompat.settings.server", "7", Arg.Is<string>(d => d.Contains("verdict=differ") && d.Contains("groups=1")));
    }

    [TestMethod]
    public void Hello_WithAGroupMissingOnOneSide_CountsItAsDivergent()
    {
        var hello = HelloFrom(new FakeSettings());
        var keep = Enumerable.Range(0, hello.GroupNames.Length).Where(i => hello.GroupNames[i] != "World").ToArray();
        hello.GroupNames = keep.Select(i => hello.GroupNames[i]).ToArray();
        hello.GroupHashes = keep.Select(i => hello.GroupHashes[i]).ToArray();

        var reply = ServerReplyTo(hello);

        CollectionAssert.AreEqual(new[] { "World" }, reply.DivergentGroups);
    }

    [TestMethod]
    public void Hello_FromAnInconclusivePeer_RepliesInconclusive_NeverMatch()
    {
        var hello = HelloFrom(new FakeSettings());
        hello.Conclusive = false;

        var reply = ServerReplyTo(hello);

        Assert.AreEqual((int)HandshakeVerdict.Inconclusive, reply.Verdict);
        _logger.Received().LogInfo("[CoopCompat] settings peer=7 inconclusive");
    }

    [TestMethod]
    public void Hello_WhenTheServerMeasuredNothing_RepliesInconclusive()
    {
        _transport = new FakeCoopTransport();
        _service = new SettingsHandshakeService(_transport, Unmeasured, _identity, _notice, _logger, _outcomes);

        var reply = ServerReplyTo(HelloFrom(new FakeSettings()));

        Assert.AreEqual((int)HandshakeVerdict.Inconclusive, reply.Verdict);
    }

    [TestMethod]
    public void Hello_WithADifferentTaomVersion_FlagsABuildMismatch()
    {
        var reply = ServerReplyTo(HelloFrom(new FakeSettings(), Identity(taom: "build.2")));

        Assert.IsTrue(reply.BuildMismatch);
    }

    [TestMethod]
    public void Hello_WithADifferentMvid_FlagsABuildMismatch()
    {
        Assert.IsTrue(ServerReplyTo(HelloFrom(new FakeSettings(), Identity(taomMvid: "other"))).BuildMismatch);
        Assert.IsTrue(ServerReplyTo(HelloFrom(new FakeSettings(), Identity(compatMvid: "other"))).BuildMismatch);
    }

    [TestMethod]
    public void Hello_WithADifferentProtocol_FlagsABuildMismatch_AndLeavesTheGroupsAlone()
    {
        var reply = ServerReplyTo(HelloFrom(new FakeSettings { Charge = false }, Identity(protocol: 99)));

        Assert.IsTrue(reply.BuildMismatch);
        Assert.AreEqual((int)HandshakeVerdict.Inconclusive, reply.Verdict);
        Assert.AreEqual(0, reply.DivergentGroups.Length);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("build mismatch") && m.Contains("peer=7")));
    }

    [TestMethod]
    public void Hello_OnAClient_IsIgnored()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseHello(FakeCoopTransport.Peer(), HelloFrom(new FakeSettings()));

        Assert.AreEqual(0, _transport.RepliesSent.Count);
    }

    [TestMethod]
    public void Reply_WithDivergentGroups_TellsThePlayerOnce_AndWarnsOnly()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            Verdict = (int)HandshakeVerdict.Differ,
            DivergentGroups = new[] { "Battle Tactics", "World" },
        });

        CollectionAssert.AreEqual(new[] { "Battle Tactics", "World" }, _notice.Differs.Single());
        Assert.AreEqual(0, _notice.BuildMismatches);
        _logger.Received().LogWarning("[CoopCompat] settings differ from the server: Battle Tactics, World");
        _outcomes.Received().Pass("taom.coopcompat.settings.client", "", Arg.Is<string>(d => d.Contains("verdict=differ")));
    }

    [TestMethod]
    public void Reply_Match_RaisesNoNotice()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, Verdict = (int)HandshakeVerdict.Match });

        Assert.AreEqual(0, _notice.Differs.Count);
        Assert.AreEqual(0, _notice.BuildMismatches);
        _logger.Received().LogInfo("[CoopCompat] settings match the server");
    }

    [TestMethod]
    public void Reply_MatchAfterTheWire_WritesTheClientPassLine()
    {
        _transport.CurrentRole = CoopRole.Client;
        var serializer = new Common.Serialization.ProtoBufSerializer(new Common.Serialization.SerializableTypeMapper());
        var sent = new CoopCompatHelloReply { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, Verdict = (int)HandshakeVerdict.Match };

        _transport.RaiseReply((CoopCompatHelloReply)serializer.Deserialize(serializer.Serialize(sent)));

        _outcomes.Received().Pass("taom.coopcompat.settings.client", "", "verdict=match groups=0 build_mismatch=false");
    }

    [TestMethod]
    public void Reply_Inconclusive_RaisesNoNotice_ButLogs()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, Verdict = (int)HandshakeVerdict.Inconclusive });

        Assert.AreEqual(0, _notice.Differs.Count);
        _logger.Received().LogInfo("[CoopCompat] settings could not be compared with the server");
    }

    [TestMethod]
    public void Reply_WithABuildMismatch_GetsItsOwnLouderNotice()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            Verdict = (int)HandshakeVerdict.Differ,
            DivergentGroups = new[] { "World" },
            BuildMismatch = true,
        });

        Assert.AreEqual(1, _notice.BuildMismatches);
        _outcomes.Received().Fail("taom.coopcompat.settings.client", "", Arg.Is<string>(d => d.Contains("build_mismatch=true")));
    }

    [TestMethod]
    public void Reply_WithAnotherProtocol_IsABuildMismatch_AndItsGroupsAreNotTrusted()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = 99, Verdict = (int)HandshakeVerdict.Differ, DivergentGroups = new[] { "World" } });

        Assert.AreEqual(1, _notice.BuildMismatches);
        Assert.AreEqual(0, _notice.Differs.Count);
    }

    [TestMethod]
    public void Reply_OnAServer_IsIgnored()
    {
        _transport.CurrentRole = CoopRole.Server;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, Verdict = (int)HandshakeVerdict.Differ, DivergentGroups = new[] { "World" } });

        Assert.AreEqual(0, _notice.Differs.Count);
    }
}
