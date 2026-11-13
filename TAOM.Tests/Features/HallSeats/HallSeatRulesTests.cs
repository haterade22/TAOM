using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Tests.Features.HallSeats;

/// <summary>
/// The two plain-data rules of Hall Seats (docs/features/hall-seats.md): how many seats the lord tags hold when one
/// machine can sit under several tags, and which lords are never held back. The engine reads stay in the adapter.
/// </summary>
[TestClass]
public class HallSeatRulesTests
{
    private static IReadOnlyList<HallSeatMachine> Tag(params HallSeatMachine[] machines) => machines;

    [TestMethod]
    public void CountSeats_NoTags_IsZero()
    {
        Assert.AreEqual(0, HallSeatRules.CountSeats(new List<IReadOnlyList<HallSeatMachine>>()));
    }

    [TestMethod]
    public void CountSeats_OneMachineUnderTwoTags_CountsOnce()
    {
        // Vanilla sp_npc_wait: npc_common_limited, npc_wait and npc_common on one entity; one spawn marks it used for every tag.
        var spot = new HallSeatMachine(new object(), 1);

        var seats = HallSeatRules.CountSeats(new[] { Tag(), Tag(spot), Tag(spot) });

        Assert.AreEqual(1, seats);
    }

    [TestMethod]
    public void CountSeats_DistinctMachines_CountEach()
    {
        var a = new HallSeatMachine(new object(), 1);
        var b = new HallSeatMachine(new object(), 1);
        var c = new HallSeatMachine(new object(), 2);

        Assert.AreEqual(3, HallSeatRules.CountSeats(new[] { Tag(a), Tag(b, c) }));
    }

    [TestMethod]
    public void CountSeats_AMachineWithSeveralFreePoints_SeatsOneAgent()
    {
        // GetInitialFrameForSpawnTag adds the whole machine to _usedSpawnPoints after one spawn.
        var table = new HallSeatMachine(new object(), 6);

        Assert.AreEqual(1, HallSeatRules.CountSeats(new[] { Tag(table) }));
    }

    [TestMethod]
    public void CountSeats_AMachineWithNoFreePoint_SeatsNobody()
    {
        var taken = new HallSeatMachine(new object(), 0);

        Assert.AreEqual(0, HallSeatRules.CountSeats(new[] { Tag(taken) }));
    }

    [TestMethod]
    public void CountSeats_SameIdentityUnderTagsWithDifferentCounts_StillCountsOnce()
    {
        var machine = new object();

        var seats = HallSeatRules.CountSeats(new[] { Tag(new HallSeatMachine(machine, 0)), Tag(new HallSeatMachine(machine, 2)) });

        Assert.AreEqual(1, seats);
    }

    [TestMethod]
    public void CountSeats_TwoEqualButDistinctObjects_AreTwoMachines()
    {
        var a = new HallSeatMachine(new string('x', 2), 1);
        var b = new HallSeatMachine(new string('x', 2), 1);

        Assert.AreEqual(2, HallSeatRules.CountSeats(new[] { Tag(a, b) }));
    }

    [TestMethod]
    public void IsMustKeep_NoTerm_IsFalse()
    {
        Assert.IsFalse(HallSeatRules.IsMustKeep(false, false, false, false, false, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_PlayerClan_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(playerClan: true, false, false, false, false, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_SettlementOwner_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, settlementOwner: true, false, false, false, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_KingdomLeader_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, kingdomLeader: true, false, false, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_Governor_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, false, governor: true, false, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_ConversationTarget_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, false, false, conversationTarget: true, false, false, false));
    }

    [TestMethod]
    public void IsMustKeep_HasIssue_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, false, false, false, hasIssue: true, false, false));
    }

    [TestMethod]
    public void IsMustKeep_QuestGiver_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, false, false, false, false, questGiver: true, false));
    }

    [TestMethod]
    public void IsMustKeep_QuestTarget_IsTrue()
    {
        Assert.IsTrue(HallSeatRules.IsMustKeep(false, false, false, false, false, false, false, questTarget: true));
    }
}
