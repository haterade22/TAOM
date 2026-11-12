using System;
using System.Collections.Generic;
using System.Linq;
using Common.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Tests.Messages;

/// <summary>Every contract through Coop's real serializer and type mapper, and no id collision with any Coop contract.</summary>
[TestClass]
public class ContractRoundTripTests
{
    private static object RoundTrip(object message)
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        return serializer.Deserialize(serializer.Serialize(message));
    }

    [TestMethod]
    public void Hello_RoundTrips()
    {
        var back = (CoopCompatHello)RoundTrip(ContractSamples.Hello());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual("v2.0.38", back.TaomVersion);
        Assert.AreEqual("11111111-2222-3333-4444-555555555555", back.TaomMvid);
        Assert.AreEqual("66666666-7777-8888-9999-000000000000", back.CoopCompatMvid);
        Assert.AreEqual("abc123", back.SettingsGlobal);
        CollectionAssert.AreEqual(new[] { "Battle Tactics", "Momentum" }, back.GroupNames);
        CollectionAssert.AreEqual(new[] { "h1", "h2" }, back.GroupHashes);
        Assert.IsTrue(back.Conclusive);
    }

    [TestMethod]
    public void HelloReply_RoundTrips()
    {
        var back = (CoopCompatHelloReply)RoundTrip(ContractSamples.Reply());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual(2, back.Verdict);
        CollectionAssert.AreEqual(new[] { "Battle Tactics" }, back.DivergentGroups);
        Assert.IsTrue(back.BuildMismatch);
    }

    [TestMethod]
    public void MomentumSnapshot_RoundTrips()
    {
        var back = (CoopCompatMomentumSnapshot)RoundTrip(ContractSamples.Snapshot());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual(9_000_000_000L, back.Sequence);
        CollectionAssert.AreEqual(new[] { "a", "b" }, back.Keys);
        CollectionAssert.AreEqual(new[] { "1", "2" }, back.Values);
    }

    [TestMethod]
    public void EmptyArrays_RoundTripAsEmpty_NeverNull()
    {
        // protobuf-net writes nothing for an empty repeated field, so only the constructor's
        // initializer can give the reader an empty array back.
        var reply = (CoopCompatHelloReply)RoundTrip(new CoopCompatHelloReply { ProtocolVersion = 1 });
        var hello = (CoopCompatHello)RoundTrip(new CoopCompatHello { ProtocolVersion = 1 });
        var snapshot = (CoopCompatMomentumSnapshot)RoundTrip(new CoopCompatMomentumSnapshot { ProtocolVersion = 1 });

        Assert.IsNotNull(reply.DivergentGroups);
        Assert.AreEqual(0, reply.DivergentGroups.Length);
        Assert.IsNotNull(hello.GroupNames);
        Assert.IsNotNull(hello.GroupHashes);
        Assert.IsNotNull(snapshot.Keys);
        Assert.IsNotNull(snapshot.Values);
    }

    [TestMethod]
    public void EveryContract_HasProtocolVersionAsFieldOne()
    {
        foreach (var message in ContractSamples.All())
        {
            var member = message.GetType().GetProperty("ProtocolVersion");
            Assert.IsNotNull(member, message.GetType().Name);
            var attr = member!.GetCustomAttributes(typeof(ProtoMemberAttribute), false).Cast<ProtoMemberAttribute>().Single();
            Assert.AreEqual(1, attr.Tag, message.GetType().Name);
        }
    }

    [TestMethod]
    public void TypeMapper_RegistersEveryContract_WithoutCollidingWithCoopOrEachOther()
    {
        // The mapper throws on a collision while it scans every loaded assembly, Coop's included.
        var mapper = new SerializableTypeMapper();
        var ids = new Dictionary<int, Type>();

        foreach (var message in ContractSamples.All())
        {
            Assert.IsTrue(mapper.TryGetId(message.GetType(), out var id), message.GetType().Name);
            Assert.IsTrue(mapper.TryGetType(id, out var type));
            Assert.AreEqual(message.GetType(), type);
            ids.Add(id, type);
        }

        Assert.AreEqual(3, ids.Count);
    }

    [TestMethod]
    public void ContractIds_DifferFromEveryOtherLoadedContractId()
    {
        var mapper = new SerializableTypeMapper();
        var ours = ContractSamples.All().Select(m => m.GetType()).ToHashSet();
        var ourIds = ours.Select(t => { mapper.TryGetId(t, out var id); return id; }).ToHashSet();

        var others = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException) { return Type.EmptyTypes; } })
            .Where(t => !ours.Contains(t) && t.FullName != null && t.IsDefined(typeof(ProtoContractAttribute), false))
            .ToList();

        Assert.IsTrue(others.Count > 0, "Coop's contracts were not loaded, so the collision check proves nothing.");
        foreach (var type in others)
        {
            mapper.TryGetId(type, out var id);
            Assert.IsFalse(ourIds.Contains(id), $"{type.FullName} shares a wire id with a CoopCompat contract.");
        }
    }
}
