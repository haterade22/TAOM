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
        CollectionAssert.AreEqual(new[] { "TAOM_Map", "TAOM" }, back.ModuleOrder);
    }

    [TestMethod]
    public void HelloReply_RoundTrips()
    {
        var back = (CoopCompatHelloReply)RoundTrip(ContractSamples.Reply());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual(2, back.Verdict);
        CollectionAssert.AreEqual(new[] { "Battle Tactics" }, back.DivergentGroups);
        Assert.IsTrue(back.BuildMismatch);
        Assert.IsTrue(back.LoadOrderDiffers);
        Assert.AreEqual("TAOM Map (TAOM_Map)", back.LoadOrderHostFirst);
        Assert.AreEqual("TAOM", back.LoadOrderHostSecond);
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
    public void LdRunEnded_RoundTrips()
    {
        var back = (CoopCompatLdRunEnded)RoundTrip(ContractSamples.LdRunEnded());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual("session-1", back.SessionId);
        Assert.AreEqual(3, back.Epoch);
        Assert.AreEqual(2, back.Reason);
        Assert.AreEqual(7, back.Wave);
        Assert.AreEqual(6, back.WavesCleared);
        Assert.AreEqual(412, back.Kills);
    }

    [TestMethod]
    public void TradeRequest_RoundTrips()
    {
        var back = (CoopCompatTradeRequest)RoundTrip(ContractSamples.TradeRequest());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual("abc", back.RequestId);
        Assert.AreEqual(4, back.Action);
        Assert.AreEqual("town_A1", back.SettlementId);
        Assert.AreEqual("mail", back.Subject);
        Assert.AreEqual("fine", back.Modifier);
        Assert.AreEqual("plate", back.Target);
        Assert.AreEqual(3, back.Quantity);
    }

    [TestMethod]
    public void TradeReply_RoundTrips()
    {
        var back = (CoopCompatTradeReply)RoundTrip(ContractSamples.TradeReply());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual("abc", back.RequestId);
        Assert.AreEqual(2, back.Action);
        Assert.AreEqual(6, back.Status);
        Assert.AreEqual(5, back.Quantity);
        Assert.AreEqual(50, back.TotalCost);
        Assert.AreEqual("Horses", back.ResourceName);
        Assert.AreEqual("horses", back.ResourceId);
        Assert.IsTrue(back.HasBalance);
        Assert.AreEqual(12.5f, back.Balance);
        Assert.IsTrue(back.HasArmouryLevel);
        Assert.AreEqual(2, back.ArmouryLevel);
        Assert.IsTrue(back.HasLadder);
        Assert.AreEqual(3, back.LadderSlot);
        Assert.IsTrue(back.LadderReady);
        Assert.IsTrue(back.LadderCanClaim);
        Assert.AreEqual(3, back.LadderLordLevel);
        Assert.AreEqual("m_gondor", back.LadderMaterialId);
        Assert.AreEqual(30, back.LadderNeeded);
        Assert.AreEqual(12, back.LadderCarried);
        CollectionAssert.AreEqual(new[] { "gd_lord_helm", "gd_lord_helm_b" }, back.LadderChoices);
    }

    [TestMethod]
    public void TradeReply_WithNoLadderChoices_RoundTripsAsAnEmptyList()
    {
        var sample = ContractSamples.TradeReply();
        sample.LadderChoices = Array.Empty<string>();

        var back = (CoopCompatTradeReply)RoundTrip(sample);

        Assert.IsNotNull(back.LadderChoices);
        Assert.AreEqual(0, back.LadderChoices.Length);
        Assert.IsNotNull(new CoopCompatTradeReply().LadderChoices, "a fresh reply holds an empty list, never null");
    }

    [TestMethod]
    public void CareerReport_RoundTrips()
    {
        var back = (CoopCompatCareerReport)RoundTrip(ContractSamples.CareerReport());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual(9_000_000_001L, back.Sequence);
        Assert.IsTrue(back.JoinReport);
        Assert.AreEqual("Player1", back.HeroId);
        Assert.AreEqual("ranger_of_ithilien", back.CareerId);
        CollectionAssert.AreEqual(new[] { "root", "fork_a" }, back.Choices, "the order is the order the choices were taken");
        CollectionAssert.AreEqual(new[] { 2, 3 }, back.Tiers);
        CollectionAssert.AreEqual(new[] { "career_tree_v2" }, back.Flags);
    }

    [TestMethod]
    public void CareerUpdate_RoundTrips()
    {
        var back = (CoopCompatCareerUpdate)RoundTrip(ContractSamples.CareerUpdate());

        Assert.AreEqual(1, back.ProtocolVersion);
        Assert.AreEqual(42, back.Sequence);
        Assert.AreEqual("Player2", back.HeroId);
        Assert.AreEqual("warden_of_fornost", back.CareerId);
        CollectionAssert.AreEqual(new[] { "root" }, back.Choices);
        CollectionAssert.AreEqual(new[] { 2 }, back.Tiers);
        CollectionAssert.AreEqual(new[] { "proven" }, back.Flags);
        Assert.IsTrue(back.Refused);
        Assert.IsTrue(back.Known);
    }

    [TestMethod]
    public void CareerContracts_EmptyFields_RoundTripAsEmpty_NeverNull()
    {
        var report = (CoopCompatCareerReport)RoundTrip(new CoopCompatCareerReport { ProtocolVersion = 1 });
        var update = (CoopCompatCareerUpdate)RoundTrip(new CoopCompatCareerUpdate { ProtocolVersion = 1 });

        Assert.AreEqual("", report.HeroId);
        Assert.AreEqual("", report.CareerId);
        Assert.AreEqual(0, report.Choices.Length);
        Assert.AreEqual(0, report.Tiers.Length);
        Assert.AreEqual(0, report.Flags.Length);
        Assert.AreEqual("", update.HeroId);
        Assert.AreEqual("", update.CareerId);
        Assert.AreEqual(0, update.Choices.Length);
        Assert.AreEqual(0, update.Tiers.Length);
        Assert.AreEqual(0, update.Flags.Length);
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
        Assert.IsNotNull(hello.ModuleOrder);
        Assert.AreEqual(0, hello.ModuleOrder.Length);
        Assert.AreEqual("", reply.LoadOrderHostFirst);
        Assert.AreEqual("", reply.LoadOrderHostSecond);
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

        Assert.AreEqual(8, ids.Count);
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
