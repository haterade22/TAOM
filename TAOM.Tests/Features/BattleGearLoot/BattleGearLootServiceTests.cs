using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.BattleGearLoot;
using TAOM.Features.BattleGearLoot.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class BattleGearLootServiceTests
{
    private sealed class FakeRandom : IRandomProvider
    {
        public readonly Queue<int> Rolls = new();
        public int Calls;

        public int Next(int maxValue)
        {
            Calls++;
            return Rolls.Dequeue();
        }
    }

    private sealed class FakeCasualty : ICasualtyGearAdapter
    {
        public IReadOnlyList<GearPiece> Pieces { get; set; } = Array.Empty<GearPiece>();
        public bool VanillaGivesNothing { get; set; }
        public List<IReadOnlyList<int>> Grants { get; } = new();

        public void Grant(IReadOnlyList<int> slots) => Grants.Add(slots);
    }

    private sealed class Rig
    {
        public BattleGearLootSettings Settings = new(true, 10);
        public readonly FakeRandom Random = new();
        public readonly IArmourAcquisitionConfigProvider ArmourConfig = Substitute.For<IArmourAcquisitionConfigProvider>();
        public readonly ICoopSessionProvider Coop = Substitute.For<ICoopSessionProvider>();
        public readonly IDedicatedServerProvider Dedicated = Substitute.For<IDedicatedServerProvider>();
        public readonly IOutcomeLog Outcomes = Substitute.For<IOutcomeLog>();

        public Rig()
        {
            Coop.IsAuthority.Returns(true);
            var d = ArmourAcquisitionConfig.Default;
            ArmourConfig.GetConfig().Returns(new ArmourAcquisitionConfig(false, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes,
                new[] { "named_blade" }, d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation,
                d.VisitChancePerDay, d.VisitDurationDays, d.VisitLevelBonus, d.Ladder));
        }

        public BattleGearLootService Build()
        {
            var settings = Substitute.For<IBattleGearLootSettingsProvider>();
            settings.Get().Returns(_ => Settings);
            return new BattleGearLootService(settings, Random, ArmourConfig, Coop, Dedicated, Outcomes);
        }
    }

    private static GearPiece P(int slot, string id = "x", bool merchandise = true) => new(slot, id, merchandise, false, false, null);

    [TestMethod]
    public void Applies_SwitchOff_False()
    {
        var rig = new Rig { Settings = new(false, 10) };
        Assert.IsFalse(rig.Build().Applies());
    }

    [TestMethod]
    public void Applies_SessionActive_False()
    {
        var rig = new Rig();
        rig.Coop.IsSessionActive.Returns(true);
        Assert.IsFalse(rig.Build().Applies());
    }

    [TestMethod]
    public void Applies_DefersToHost_False()
    {
        var rig = new Rig();
        rig.Coop.ShouldDeferToHost.Returns(true);
        Assert.IsFalse(rig.Build().Applies());
    }

    [TestMethod]
    public void Applies_DedicatedServer_False()
    {
        var rig = new Rig();
        rig.Dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(rig.Build().Applies());
    }

    [TestMethod]
    public void Applies_SinglePlayer_True() => Assert.IsTrue(new Rig().Build().Applies());

    [TestMethod]
    public void LootCasualty_VanillaGivesNothing_GrantsNothing_LogsNothing()
    {
        var rig = new Rig();
        var c = new FakeCasualty { VanillaGivesNothing = true, Pieces = new[] { P(0) } };

        rig.Build().LootCasualty(c, "b1");
        Assert.AreEqual(0, c.Grants.Count);
        Assert.AreEqual(0, rig.Random.Calls);
        rig.Outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void PickSlots_Chance0_PicksNone_DrawsNoRoll()
    {
        var rig = new Rig();
        var picked = rig.Build().PickSlots(new[] { P(0), P(1) }, 0);
        Assert.AreEqual(0, picked.Count);
        Assert.AreEqual(0, rig.Random.Calls);
    }

    [TestMethod]
    public void PickSlots_Chance100_PicksEveryEligible_DrawsNoRoll()
    {
        var rig = new Rig();
        var picked = rig.Build().PickSlots(new[] { P(0), P(3), P(5, merchandise: false) }, 100);
        CollectionAssert.AreEqual(new[] { 0, 3 }, picked.ToArray());
        Assert.AreEqual(0, rig.Random.Calls);
    }

    [TestMethod]
    public void PickSlots_Chance10_PicksOnlyRollsBelow10()
    {
        var rig = new Rig();
        foreach (var r in new[] { 5, 50, 9, 10 })
            rig.Random.Rolls.Enqueue(r);

        var picked = rig.Build().PickSlots(new[] { P(0), P(1), P(2), P(3) }, 10);
        CollectionAssert.AreEqual(new[] { 0, 2 }, picked.ToArray());
    }

    [TestMethod]
    public void PickSlots_OneRollPerEligiblePiece_InSlotOrder()
    {
        var rig = new Rig();
        rig.Random.Rolls.Enqueue(99);
        rig.Random.Rolls.Enqueue(0);
        rig.Random.Rolls.Enqueue(99);

        var picked = rig.Build().PickSlots(new[] { P(0), P(1), P(2) }, 50);
        CollectionAssert.AreEqual(new[] { 1 }, picked.ToArray());
        Assert.AreEqual(3, rig.Random.Calls);
    }

    [TestMethod]
    public void PickSlots_ExcludedPiece_ConsumesNoRoll()
    {
        var rig = new Rig();
        rig.Random.Rolls.Enqueue(0);

        var picked = rig.Build().PickSlots(new[] { P(0, merchandise: false), P(1) }, 50);
        CollectionAssert.AreEqual(new[] { 1 }, picked.ToArray());
        Assert.AreEqual(1, rig.Random.Calls);
    }

    [TestMethod]
    public void PickSlots_NamedWeapon_ExcludedWhateverTheArmourGateState()
    {
        var rig = new Rig();

        var picked = rig.Build().PickSlots(new[] { P(0, "named_blade") }, 100);
        Assert.AreEqual(0, picked.Count);
    }

    [TestMethod]
    public void PickSlots_SameItemInTwoSlots_RollsAndGrantsTwice()
    {
        var rig = new Rig();
        rig.Random.Rolls.Enqueue(0);
        rig.Random.Rolls.Enqueue(0);

        var picked = rig.Build().PickSlots(new[] { P(0, "javelin"), P(1, "javelin") }, 50);
        CollectionAssert.AreEqual(new[] { 0, 1 }, picked.ToArray());
    }

    [TestMethod]
    public void LootCasualty_NoPieces_GrantsNothing()
    {
        var rig = new Rig();
        var c = new FakeCasualty();

        rig.Build().LootCasualty(c, "b1");
        Assert.AreEqual(0, c.Grants.Count);
    }

    [TestMethod]
    public void LootCasualty_CallsGrantOnce_WithPickedSlots()
    {
        var rig = new Rig { Settings = new(true, 100) };
        var c = new FakeCasualty { Pieces = new[] { P(0), P(2) } };

        rig.Build().LootCasualty(c, "b1");
        Assert.AreEqual(1, c.Grants.Count);
        CollectionAssert.AreEqual(new[] { 0, 2 }, c.Grants[0].ToArray());
    }

    [TestMethod]
    public void LootCasualty_WritesTheAppliedOutcomeUnderTheBattleKey()
    {
        var rig = new Rig { Settings = new(true, 100) };
        var service = rig.Build();

        service.LootCasualty(new FakeCasualty { Pieces = new[] { P(0) } }, "b1");
        service.LootCasualty(new FakeCasualty { Pieces = new[] { P(0) } }, "b1");
        service.LootCasualty(new FakeCasualty { Pieces = new[] { P(0) } }, "b2");

        rig.Outcomes.Received(2).Pass("taom.battle-gear-loot.applied", "b1", "chance=100");
        rig.Outcomes.Received(1).Pass("taom.battle-gear-loot.applied", "b2", "chance=100");
    }
}
