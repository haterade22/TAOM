using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class PersistedEquipmentIssueLedgerTests
{
    private EnlistmentContentStore _store = null!;
    private PersistedEquipmentIssueLedger _ledger = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentContentStore(Substitute.For<IModLogger>());
        _ledger = new PersistedEquipmentIssueLedger(_store);
    }

    [TestMethod]
    public void Fresh_NothingIssued()
    {
        Assert.IsNull(_ledger.HighestIssuedRank);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentRank.Recruit));
    }

    [TestMethod]
    public void RecordIssue_MarksRankAndKeepsItemInstances()
    {
        _ledger.RecordIssue(EnlistmentRank.Recruit, new[] { "sk_a", "sk_b", "sk_b" });

        Assert.AreEqual(EnlistmentRank.Recruit, _ledger.HighestIssuedRank);
        Assert.AreEqual(3, _ledger.IssuedItemIds.Count, "duplicate instances preserved for the payoff sum");
        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentRank.Recruit));
    }

    [TestMethod]
    public void HasIssued_IsMonotonic_DemotionNeverReIssues()
    {
        _ledger.RecordIssue(EnlistmentRank.Veteran, new[] { "sk_v" });

        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentRank.Recruit));
        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentRank.Soldier));
        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentRank.Veteran));
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentRank.Sergeant));
    }

    [TestMethod]
    public void RecordIssue_LowerRankAfterHigher_DoesNotLowerTheWatermark()
    {
        _ledger.RecordIssue(EnlistmentRank.Sergeant, new[] { "sk_s" });
        _ledger.RecordIssue(EnlistmentRank.Recruit, new[] { "sk_r" });

        Assert.AreEqual(EnlistmentRank.Sergeant, _ledger.HighestIssuedRank);
    }

    [TestMethod]
    public void SurvivesSaveLoadRoundTrip()
    {
        // The whole point of the persisted ledger: a full restart must NOT re-allow a draw.
        _ledger.RecordIssue(EnlistmentRank.Soldier, new[] { "sk_head", "sk_body" });
        var section = _store.Serialize();

        var reloadedStore = new EnlistmentContentStore(Substitute.For<IModLogger>());
        reloadedStore.Deserialize(section);
        var reloadedLedger = new PersistedEquipmentIssueLedger(reloadedStore);

        Assert.AreEqual(EnlistmentRank.Soldier, reloadedLedger.HighestIssuedRank);
        CollectionAssert.AreEqual(new[] { "sk_head", "sk_body" }, reloadedLedger.IssuedItemIds.ToArray());
        Assert.IsTrue(reloadedLedger.HasIssuedForRank(EnlistmentRank.Soldier));
    }

    [TestMethod]
    public void Reset_ClearsBothFields()
    {
        _ledger.RecordIssue(EnlistmentRank.Veteran, new[] { "sk_v" });

        _ledger.Reset();

        Assert.IsNull(_ledger.HighestIssuedRank);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
    }

    [TestMethod]
    public void DischargeClearsRecord_ClearsLedgerToo()
    {
        _ledger.RecordIssue(EnlistmentRank.Veteran, new[] { "sk_v" });

        _store.Clear(); // what the discharge consequence service does

        Assert.IsNull(_ledger.HighestIssuedRank, "a new term starts with no kit drawn");
    }

    [TestMethod]
    public void UndefinedRankOrdinalInSave_TreatedAsNothingIssued()
    {
        _store.Record.HighestIssuedEquipmentRank = 99;

        Assert.IsNull(_ledger.HighestIssuedRank);
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentRank.Recruit));
    }
}
