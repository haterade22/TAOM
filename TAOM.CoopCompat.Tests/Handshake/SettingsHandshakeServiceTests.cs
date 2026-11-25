using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.CoopCompat.Handshake;
using TAOM.CoopCompat.LoadOrder;
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
        public List<(string First, string Second)> LoadOrders { get; } = new();

        public void SettingsDiffer(IReadOnlyList<string> groups) => Differs.Add(groups.ToArray());

        public void BuildMismatch() => BuildMismatches++;

        public void LoadOrderDiffers(string hostFirst, string hostSecond) => LoadOrders.Add((hostFirst, hostSecond));
    }

    // The host's modules, in load order.
    private sealed class FixedModules : ILoadOrderAdapter
    {
        public IReadOnlyList<ModuleEntry> ActiveModules() => new[]
        {
            new ModuleEntry("Native", "Native", true, true),
            new ModuleEntry("TAOM_Map", "TAOM Map", false, true),
            new ModuleEntry("LOTRLOME_Armory", "LOTR Armory", false, true),
            new ModuleEntry("TAOM", "TAOM", false, true),
            new ModuleEntry("RBM", "Realistic Battle", false, true),
        };
    }

    private static readonly string[] HostOrder = { "Native", "TAOM_Map", "LOTRLOME_Armory", "TAOM", "RBM" };
    private static readonly string[] SwappedOrder = { "Native", "TAOM_Map", "RBM", "LOTRLOME_Armory", "TAOM" };

    private FakeCoopTransport _transport = null!;
    private RecordingNotice _notice = null!;
    private IModLogger _logger = null!;
    private IOutcomeLog _outcomes = null!;
    private FakeSettings _local = null!;
    private BuildIdentity _identity = null!;
    private bool _loadOrderWarning;
    private LoadOrderCheckService _loadOrder = null!;
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
        _loadOrderWarning = true;
        _loadOrder = new LoadOrderCheckService(new FixedModules(), () => _loadOrderWarning, _logger);
        _service = new SettingsHandshakeService(_transport, () => SettingsFingerprint.ComputeAcross(_local), _identity, _loadOrder, _notice, _logger, _outcomes);
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
            ModuleOrder = HostOrder,
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
        _service = new SettingsHandshakeService(_transport, Unmeasured, _identity, _loadOrder, _notice, _logger, _outcomes);
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
        _service = new SettingsHandshakeService(_transport, Unmeasured, _identity, _loadOrder, _notice, _logger, _outcomes);

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

    [TestMethod]
    public void ClientReady_SendsTheLocalModuleOrder()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseClientReady();

        CollectionAssert.AreEqual(HostOrder, _transport.HellosSent.Single().ModuleOrder);
    }

    [TestMethod]
    public void Hello_WithTheSameLoadOrder_RepliesNoLoadOrderWarning()
    {
        var reply = ServerReplyTo(HelloFrom(new FakeSettings()));

        Assert.IsFalse(reply.LoadOrderDiffers);
        Assert.AreEqual("", reply.LoadOrderHostFirst);
        _logger.Received().LogInfo("[CoopCompat] load order peer=7 match");
    }

    [TestMethod]
    public void Hello_WithAnotherLoadOrder_RepliesTheFirstPair_AndKeepsTheSettingsVerdict()
    {
        var hello = HelloFrom(new FakeSettings());
        hello.ModuleOrder = SwappedOrder;

        var reply = ServerReplyTo(hello);

        Assert.IsTrue(reply.LoadOrderDiffers);
        Assert.AreEqual("LOTR Armory (LOTRLOME_Armory)", reply.LoadOrderHostFirst);
        Assert.AreEqual("Realistic Battle (RBM)", reply.LoadOrderHostSecond);
        Assert.AreEqual((int)HandshakeVerdict.Match, reply.Verdict);
        Assert.IsFalse(reply.BuildMismatch);
    }

    [TestMethod]
    public void Hello_FromAnOlderPeer_WithoutAModuleOrder_RepliesNoLoadOrderWarning()
    {
        var hello = HelloFrom(new FakeSettings());
        hello.ModuleOrder = Array.Empty<string>();

        var reply = ServerReplyTo(hello);

        Assert.IsFalse(reply.LoadOrderDiffers);
        Assert.AreEqual((int)HandshakeVerdict.Match, reply.Verdict);
    }

    [TestMethod]
    public void Hello_WithTheHostSwitchOff_RepliesNoLoadOrderWarning_ReadPerHello()
    {
        var hello = HelloFrom(new FakeSettings());
        hello.ModuleOrder = SwappedOrder;

        _loadOrderWarning = false;
        Assert.IsFalse(ServerReplyTo(hello).LoadOrderDiffers);
        _loadOrderWarning = true;
        Assert.IsTrue(ServerReplyTo(hello).LoadOrderDiffers);
    }

    [TestMethod]
    public void Hello_WithAnotherProtocol_ComparesNoLoadOrder()
    {
        var hello = HelloFrom(new FakeSettings(), Identity(protocol: 99));
        hello.ModuleOrder = SwappedOrder;

        Assert.IsFalse(ServerReplyTo(hello).LoadOrderDiffers);
    }

    [TestMethod]
    public void Reply_WithALoadOrderDifference_TellsThePlayerOnce()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            Verdict = (int)HandshakeVerdict.Match,
            LoadOrderDiffers = true,
            LoadOrderHostFirst = "LOTR Armory (LOTRLOME_Armory)",
            LoadOrderHostSecond = "Realistic Battle (RBM)",
        });

        Assert.AreEqual(("LOTR Armory (LOTRLOME_Armory)", "Realistic Battle (RBM)"), _notice.LoadOrders.Single());
        Assert.AreEqual(0, _notice.Differs.Count);
        Assert.AreEqual(0, _notice.BuildMismatches);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("load order differs from the server")));
    }

    [TestMethod]
    public void Reply_WithoutALoadOrderDifference_RaisesNoLoadOrderNotice()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = CoopCompatProtocol.ProtocolVersion, Verdict = (int)HandshakeVerdict.Match });

        Assert.AreEqual(0, _notice.LoadOrders.Count);
    }

    [TestMethod]
    public void Reply_WithALoadOrderDifference_ClipsLongNames()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            LoadOrderDiffers = true,
            LoadOrderHostFirst = new string('a', 500),
            LoadOrderHostSecond = null!,
        });

        Assert.AreEqual(LoadOrderRules.MaxIdLength, _notice.LoadOrders.Single().First.Length);
        Assert.AreEqual("", _notice.LoadOrders.Single().Second);
    }

    [TestMethod]
    public void Reply_FromAnotherProtocol_RaisesNoLoadOrderNotice()
    {
        _transport.CurrentRole = CoopRole.Client;

        _transport.RaiseReply(new CoopCompatHelloReply { ProtocolVersion = 99, LoadOrderDiffers = true, LoadOrderHostFirst = "A", LoadOrderHostSecond = "B" });

        Assert.AreEqual(0, _notice.LoadOrders.Count);
    }
}
