using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>The probe-to-snapshot mapping, through the real service with a substituted probe adapter.</summary>
[TestClass]
public class ArmyRhythmSnapshotServiceTests
{
    private static ArmyRhythmSnapshot Snapshot(ArmyRhythmProbe probe)
    {
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord { CommanderHeroId = "lord_1_1" });
        var adapter = Substitute.For<IArmyRhythmProbeAdapter>();
        adapter.Probe("lord_1_1").Returns(probe);
        return new ArmyRhythmSnapshotService(store, adapter).GetSnapshot(10.0);
    }

    [TestMethod]
    public void EveryProbeFlagCombination_MapsToTheSevenSnapshotFields()
    {
        for (var bits = 0; bits < 128; bits++)
        {
            var probe = new ArmyRhythmProbe
            {
                InArmy = (bits & 1) != 0,
                InSiege = (bits & 2) != 0,
                AtSea = (bits & 4) != 0,
                Blockade = (bits & 8) != 0,
                InSettlement = (bits & 16) != 0,
                Moving = (bits & 32) != 0,
                EnemyPartyNearby = (bits & 64) != 0,
            };

            var snapshot = Snapshot(probe);

            var label = "probe bits " + bits;
            Assert.AreEqual(probe.InArmy, snapshot.InArmy, label);
            Assert.AreEqual(probe.InSiege, snapshot.SiegePressure, label);
            Assert.AreEqual(probe.AtSea, snapshot.Naval, label);
            Assert.AreEqual(probe.Blockade, snapshot.Blockade, label);
            Assert.AreEqual(probe.Moving, snapshot.Marching, label);
            Assert.AreEqual(probe.EnemyPartyNearby, snapshot.PreBattle, label);
            Assert.AreEqual(probe.InSettlement && !probe.InSiege && !probe.EnemyPartyNearby, snapshot.QuietGarrison, label);
        }
    }

    [DataTestMethod]
    [DataRow(true, false, false, true)]
    [DataRow(true, true, false, false)]
    [DataRow(true, false, true, false)]
    [DataRow(true, true, true, false)]
    [DataRow(false, false, false, false)]
    [DataRow(false, true, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, true, true, false)]
    public void QuietGarrison_NeedsASettlementWithNoSiegeAndNoEnemyNearby(bool inSettlement, bool inSiege, bool enemyNearby, bool expected)
    {
        var snapshot = Snapshot(new ArmyRhythmProbe { InSettlement = inSettlement, InSiege = inSiege, EnemyPartyNearby = enemyNearby });

        Assert.AreEqual(expected, snapshot.QuietGarrison);
    }

    [TestMethod]
    public void PreBattle_FollowsTheEnemyScanAlone()
    {
        Assert.IsTrue(Snapshot(new ArmyRhythmProbe { EnemyPartyNearby = true }).PreBattle);
        Assert.IsFalse(Snapshot(new ArmyRhythmProbe { InSiege = true, Moving = true }).PreBattle);
    }
}
