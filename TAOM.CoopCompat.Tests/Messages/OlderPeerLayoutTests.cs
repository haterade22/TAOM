using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using ProtoBuf.Meta;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Tests.Messages;

/// <summary>
/// A v2.0.40 peer knows the hello as fields 1 to 8 and the reply as fields 1 to 4. The load order fields were added
/// after them, with ProtocolVersion unchanged, so both directions must still read. The old layouts are built in a
/// model of their own, with no [ProtoContract] attribute, so Coop's type mapper never sees a second contract.
/// The real types are read through protobuf-net's default model, the one Coop's serializer uses for a message body.
/// </summary>
[TestClass]
public class OlderPeerLayoutTests
{
    // Public: protobuf-net compiles a serializer for each type.
    public sealed class V2040Hello
    {
        public int ProtocolVersion { get; set; }
        public string TaomVersion { get; set; } = "";
        public string TaomMvid { get; set; } = "";
        public string CoopCompatMvid { get; set; } = "";
        public string SettingsGlobal { get; set; } = "";
        public string[] GroupNames { get; set; } = Array.Empty<string>();
        public string[] GroupHashes { get; set; } = Array.Empty<string>();
        public bool Conclusive { get; set; }
    }

    public sealed class V2040Reply
    {
        public int ProtocolVersion { get; set; }
        public int Verdict { get; set; }
        public string[] DivergentGroups { get; set; } = Array.Empty<string>();
        public bool BuildMismatch { get; set; }
    }

    private static RuntimeTypeModel V2040Model()
    {
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(V2040Hello), false).Add(1, "ProtocolVersion").Add(2, "TaomVersion").Add(3, "TaomMvid").Add(4, "CoopCompatMvid")
            .Add(5, "SettingsGlobal").Add(6, "GroupNames").Add(7, "GroupHashes").Add(8, "Conclusive");
        model.Add(typeof(V2040Reply), false).Add(1, "ProtocolVersion").Add(2, "Verdict").Add(3, "DivergentGroups").Add(4, "BuildMismatch");
        return model;
    }

    private static object ReadAsCurrent(RuntimeTypeModel oldModel, object oldMessage, Type current)
    {
        using var stream = new MemoryStream();
        oldModel.Serialize(stream, oldMessage);
        stream.Position = 0;
        return Serializer.Deserialize(current, stream);
    }

    private static T ReadAsV2040<T>(RuntimeTypeModel oldModel, object current)
    {
        using var stream = new MemoryStream();
        Serializer.NonGeneric.Serialize(stream, current);
        stream.Position = 0;
        return (T)oldModel.Deserialize(stream, null, typeof(T));
    }

    [TestMethod]
    public void Hello_FromAV2040Peer_ReadsWithAnEmptyModuleOrder()
    {
        var old = new V2040Hello
        {
            ProtocolVersion = 1, TaomVersion = "v2.0.40", TaomMvid = "m", CoopCompatMvid = "c", SettingsGlobal = "g",
            GroupNames = new[] { "World" }, GroupHashes = new[] { "h" }, Conclusive = true,
        };

        var hello = (CoopCompatHello)ReadAsCurrent(V2040Model(), old, typeof(CoopCompatHello));

        Assert.AreEqual(1, hello.ProtocolVersion);
        Assert.AreEqual("v2.0.40", hello.TaomVersion);
        CollectionAssert.AreEqual(new[] { "World" }, hello.GroupNames);
        Assert.IsTrue(hello.Conclusive);
        Assert.IsNotNull(hello.ModuleOrder);
        Assert.AreEqual(0, hello.ModuleOrder.Length);
    }

    [TestMethod]
    public void Hello_ReadByAV2040Peer_KeepsEveryOldField()
    {
        var old = ReadAsV2040<V2040Hello>(V2040Model(), ContractSamples.Hello());

        Assert.AreEqual(1, old.ProtocolVersion);
        Assert.AreEqual("v2.0.38", old.TaomVersion);
        Assert.AreEqual("abc123", old.SettingsGlobal);
        CollectionAssert.AreEqual(new[] { "Battle Tactics", "Momentum" }, old.GroupNames);
        CollectionAssert.AreEqual(new[] { "h1", "h2" }, old.GroupHashes);
        Assert.IsTrue(old.Conclusive);
    }

    [TestMethod]
    public void Reply_FromAV2040Peer_ReadsAsNoLoadOrderWarning()
    {
        var old = new V2040Reply { ProtocolVersion = 1, Verdict = 1, DivergentGroups = new[] { "World" }, BuildMismatch = true };

        var reply = (CoopCompatHelloReply)ReadAsCurrent(V2040Model(), old, typeof(CoopCompatHelloReply));

        Assert.AreEqual(1, reply.Verdict);
        CollectionAssert.AreEqual(new[] { "World" }, reply.DivergentGroups);
        Assert.IsTrue(reply.BuildMismatch);
        Assert.IsFalse(reply.LoadOrderDiffers);
        Assert.AreEqual("", reply.LoadOrderHostFirst);
        Assert.AreEqual("", reply.LoadOrderHostSecond);
    }

    [TestMethod]
    public void Reply_ReadByAV2040Peer_KeepsEveryOldField()
    {
        var old = ReadAsV2040<V2040Reply>(V2040Model(), ContractSamples.Reply());

        Assert.AreEqual(1, old.ProtocolVersion);
        Assert.AreEqual(2, old.Verdict);
        CollectionAssert.AreEqual(new[] { "Battle Tactics" }, old.DivergentGroups);
        Assert.IsTrue(old.BuildMismatch);
    }
}
