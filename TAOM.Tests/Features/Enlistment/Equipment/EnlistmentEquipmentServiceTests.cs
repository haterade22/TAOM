using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment.Equipment;

/// <summary>
/// Issuance contract: resolve the (culture, assignment, rank) roster via the fallback chain,
/// guard every item id through IItemPoolAdapter, add surviving items to the party INVENTORY
/// (never equip), record in the ledger, once per rank. Every non-Issued outcome must leave the
/// ledger untouched so a later retry can succeed.
///
/// These use Infantry throughout: which assignment is passed is the resolver's business and is
/// covered by EnlistmentRosterResolverTests. What is pinned HERE is that the ledger stays keyed
/// on rank alone, so a role swap does not re-open a spent draw
/// (IssueForRank_SameRankDifferentAssignment_ReturnsAlreadyIssued).
/// </summary>
[TestClass]
public class EnlistmentEquipmentServiceTests
{
    private IEquipmentRosterCatalogAdapter _catalog = null!;
    private IPartyItemRosterAdapter _partyItems = null!;
    private IItemPoolAdapter _itemPool = null!;
    private InMemoryEquipmentIssueLedger _ledger = null!;
    private IModLogger _logger = null!;
    private EnlistmentEquipmentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _catalog = Substitute.For<IEquipmentRosterCatalogAdapter>();
        _partyItems = Substitute.For<IPartyItemRosterAdapter>();
        _itemPool = Substitute.For<IItemPoolAdapter>();
        _ledger = new InMemoryEquipmentIssueLedger();
        _logger = Substitute.For<IModLogger>();
        _service = new EnlistmentEquipmentService(_catalog, _partyItems, _itemPool, _ledger, _logger);

        // Defaults: everything healthy; individual tests break specific links.
        _partyItems.IsMainPartyAvailable().Returns(true);
        _partyItems.AddItem(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _itemPool.ItemExists(Arg.Any<string>()).Returns(true);
    }

    private void SetupRoster(string rosterId, params string[] itemIds)
    {
        _catalog.RosterExists(rosterId).Returns(true);
        _catalog.GetBattleSetItemIds(rosterId).Returns(itemIds);
    }

    [TestMethod]
    public void IssueForRank_HappyPath_AddsEachItemToInventory_ReturnsIssued()
    {
        SetupRoster("enlist_vlandia_infantry_recruit", "helm_a", "chest_a", "boots_a");

        var result = _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.Issued, result);
        _partyItems.Received(1).AddItem("helm_a", 1);
        _partyItems.Received(1).AddItem("chest_a", 1);
        _partyItems.Received(1).AddItem("boots_a", 1);
        Assert.AreEqual(EnlistmentRank.Recruit, _ledger.HighestIssuedRank);
        CollectionAssert.AreEqual(new[] { "helm_a", "chest_a", "boots_a" },
            (System.Collections.ICollection)_ledger.IssuedItemIds);
    }

    [TestMethod]
    public void IssueForRank_SecondCallSameRank_ReturnsAlreadyIssued_NoSecondAdd()
    {
        SetupRoster("enlist_vlandia_infantry_recruit", "chest_a");
        _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Recruit);
        _partyItems.ClearReceivedCalls();

        var result = _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.AlreadyIssuedForRank, result);
        _partyItems.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void IssueForRank_SameRankDifferentAssignment_ReturnsAlreadyIssued()
    {
        // The ledger is keyed on RANK alone, so swapping role does not re-open a spent draw.
        // Assignment swaps cost trust and sit behind a cooldown, but they are not free, and a
        // per-(assignment, rank) ledger would turn each one into another full kit.
        SetupRoster("enlist_vlandia_infantry_recruit", "chest_a");
        SetupRoster("enlist_vlandia_archer_recruit", "bow_a");
        _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Recruit);
        _partyItems.ClearReceivedCalls();

        var result = _service.IssueForRank("vlandia", ServiceAssignment.Archer, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.AlreadyIssuedForRank, result);
        _partyItems.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void IssueForRank_LowerRankAfterHigher_ReturnsAlreadyIssued()
    {
        SetupRoster("enlist_vlandia_infantry_veteran", "chest_v");
        _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Veteran);

        var result = _service.IssueForRank("vlandia", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.AlreadyIssuedForRank, result);
    }

    [TestMethod]
    public void IssueForRank_NoRosterAnywhere_ReturnsNoRosterFound_LedgerUntouched()
    {
        // RosterExists defaults to false for every id.
        var result = _service.IssueForRank("lothlorien", ServiceAssignment.Infantry, EnlistmentRank.Soldier);

        Assert.AreEqual(EquipmentIssueResult.NoRosterFound, result);
        Assert.IsNull(_ledger.HighestIssuedRank);
        _partyItems.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void IssueForRank_ExactMissing_IssuesFromFallbackDefault()
    {
        SetupRoster("enlist_default_infantry_soldier", "chest_d");

        var result = _service.IssueForRank("lothlorien", ServiceAssignment.Infantry, EnlistmentRank.Soldier);

        Assert.AreEqual(EquipmentIssueResult.Issued, result);
        _partyItems.Received(1).AddItem("chest_d", 1);
    }

    [TestMethod]
    public void IssueForRank_MissingItem_SkippedWithWarning_OthersStillIssued()
    {
        SetupRoster("enlist_gondor_infantry_recruit", "chest_ok", "ghost_item", "boots_ok");
        _itemPool.ItemExists("ghost_item").Returns(false);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.Issued, result);
        _partyItems.Received(1).AddItem("chest_ok", 1);
        _partyItems.Received(1).AddItem("boots_ok", 1);
        _partyItems.DidNotReceive().AddItem("ghost_item", Arg.Any<int>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("ghost_item")));
        CollectionAssert.DoesNotContain((System.Collections.ICollection)_ledger.IssuedItemIds, "ghost_item");
    }

    [TestMethod]
    public void IssueForRank_AllItemsMissing_ReturnsNoValidItems_LedgerUntouched()
    {
        SetupRoster("enlist_gondor_infantry_recruit", "ghost_a", "ghost_b");
        _itemPool.ItemExists(Arg.Any<string>()).Returns(false);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.NoValidItems, result);
        Assert.IsNull(_ledger.HighestIssuedRank);
        _partyItems.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void IssueForRank_EmptyRoster_ReturnsNoValidItems()
    {
        SetupRoster("enlist_gondor_infantry_recruit" /* no items */);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.NoValidItems, result);
    }

    [TestMethod]
    public void IssueForRank_PartyUnavailable_ReturnsPartyUnavailable_LedgerUntouched()
    {
        SetupRoster("enlist_gondor_infantry_recruit", "chest_a");
        _partyItems.IsMainPartyAvailable().Returns(false);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.PartyUnavailable, result);
        Assert.IsNull(_ledger.HighestIssuedRank);
        _partyItems.DidNotReceive().AddItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void IssueForRank_EveryAddFails_ReturnsPartyUnavailable_LedgerUntouched()
    {
        // Party vanished between the availability check and the adds.
        SetupRoster("enlist_gondor_infantry_recruit", "chest_a", "boots_a");
        _partyItems.AddItem(Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.PartyUnavailable, result);
        Assert.IsNull(_ledger.HighestIssuedRank);
    }

    [TestMethod]
    public void IssueForRank_PartialAddFailure_LedgerRecordsOnlyDeliveredItems()
    {
        SetupRoster("enlist_gondor_infantry_recruit", "chest_a", "boots_a");
        _partyItems.AddItem("boots_a", 1).Returns(false);

        var result = _service.IssueForRank("gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit);

        Assert.AreEqual(EquipmentIssueResult.Issued, result);
        CollectionAssert.AreEqual(new[] { "chest_a" },
            (System.Collections.ICollection)_ledger.IssuedItemIds);
    }
}
