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
        Assert.IsNull(_ledger.HighestIssuedTier);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
        Assert.IsFalse(_ledger.HasIssuedForTier(EnlistmentKitTier.Recruit));
    }

    [TestMethod]
    public void RecordIssue_MarksRankAndKeepsItemInstances()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, new[] { "sk_a", "sk_b", "sk_b" });

        Assert.AreEqual(EnlistmentKitTier.Recruit, _ledger.HighestIssuedTier);
        Assert.AreEqual(3, _ledger.IssuedItemIds.Count, "duplicate instances preserved for the per-item reclaim");
        Assert.IsTrue(_ledger.HasIssuedForTier(EnlistmentKitTier.Recruit));
    }

    [TestMethod]
    public void HasIssued_IsMonotonic_DemotionNeverReIssues()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Veteran, new[] { "sk_v" });

        Assert.IsTrue(_ledger.HasIssuedForTier(EnlistmentKitTier.Recruit));
        Assert.IsTrue(_ledger.HasIssuedForTier(EnlistmentKitTier.Soldier));
        Assert.IsTrue(_ledger.HasIssuedForTier(EnlistmentKitTier.Veteran));
        Assert.IsFalse(_ledger.HasIssuedForTier(EnlistmentKitTier.Sergeant));
    }

    [TestMethod]
    public void RecordIssue_LowerRankAfterHigher_DoesNotLowerTheWatermark()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Sergeant, new[] { "sk_s" });
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, new[] { "sk_r" });

        Assert.AreEqual(EnlistmentKitTier.Sergeant, _ledger.HighestIssuedTier);
    }

    [TestMethod]
    public void SurvivesSaveLoadRoundTrip()
    {
        // The whole point of the persisted ledger: a full restart must NOT re-allow a draw.
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new[] { "sk_head", "sk_body" });
        var section = _store.Serialize();

        var reloadedStore = new EnlistmentContentStore(Substitute.For<IModLogger>());
        reloadedStore.Deserialize(section);
        var reloadedLedger = new PersistedEquipmentIssueLedger(reloadedStore);

        Assert.AreEqual(EnlistmentKitTier.Soldier, reloadedLedger.HighestIssuedTier);
        CollectionAssert.AreEqual(new[] { "sk_head", "sk_body" }, reloadedLedger.IssuedItemIds.ToArray());
        Assert.IsTrue(reloadedLedger.HasIssuedForTier(EnlistmentKitTier.Soldier));
    }

    [TestMethod]
    public void RecordIssue_AccumulatesDuplicateItemInstances()
    {
        // The same armor piece issued at two ranks = two physical items to reclaim.
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, new[] { "item_a" });
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new[] { "item_a", "item_c" });

        CollectionAssert.AreEqual(new[] { "item_a", "item_a", "item_c" },
            (System.Collections.ICollection)_ledger.IssuedItemIds);
    }

    [TestMethod]
    public void RecordIssue_NullOrEmptyItemLists_ToleratedButRankStillRecorded()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, null);
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new string[0]);

        Assert.AreEqual(EnlistmentKitTier.Soldier, _ledger.HighestIssuedTier);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
    }

    [TestMethod]
    public void Reset_ClearsBothFields()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Veteran, new[] { "sk_v" });

        _ledger.Reset();

        Assert.IsNull(_ledger.HighestIssuedTier);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
    }

    [TestMethod]
    public void DischargeClearsRecord_ClearsLedgerToo()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Veteran, new[] { "sk_v" });

        _store.Clear(); // what the discharge consequence service does

        Assert.IsNull(_ledger.HighestIssuedTier, "a new term starts with no kit drawn");
    }

    [TestMethod]
    public void UndefinedRankOrdinalInSave_TreatedAsNothingIssued()
    {
        _store.Record.HighestIssuedKitTier = 99;

        Assert.IsNull(_ledger.HighestIssuedTier);
        Assert.IsFalse(_ledger.HasIssuedForTier(EnlistmentKitTier.Recruit));
    }
}
