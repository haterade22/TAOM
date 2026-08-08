using System;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

// DryIoc and NSubstitute both export an `Arg` type, and this file needs DryIoc for the
// container wiring-seam test at the bottom. Every `Arg` here is an argument matcher.
using Arg = NSubstitute.Arg;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// End-of-service consequence policy. Two halves:
///
/// (1) ARREARS — honourable exits settle what the column still owes, desertion forfeits it,
///     the two quiet bookkeeping exits do neither. Regression-pinned here because the
///     wage-forfeit bug (every release classified Desertion) was reported from live play.
///
/// (2) KIT RECLAIM — issued gear lands in the player's INVENTORY and is never equipped, so
///     without a reclaim the quartermaster is a free-gold vending machine: draw a kit at each
///     rank, sell it, walk out. Honourable exits hand it back. The LEDGER is not the authority
///     on what was taken — <see cref="IPartyItemRosterAdapter.RemoveItem"/>'s return value is,
///     because it drains only the unmodified stack and therefore cannot touch gear the player
///     improved, equipped, sold or lost.
/// </summary>
[TestClass]
public class DischargeConsequenceServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IServiceRewardService _rewards = null!;
    private PersistedEquipmentIssueLedger _ledger = null!;
    private IPartyItemRosterAdapter _partyItems = null!;
    private IEnlistmentFeatureSettingsProvider _feature = null!;
    private IInquiryAdapter _inquiry = null!;
    private DischargeConsequenceService _service = null!;

    /// <summary>The reasons the service treats as an honourable end of service.</summary>
    private static readonly DischargeReason[] HonourableReasons =
    {
        DischargeReason.PlayerRequest,
        DischargeReason.Commission,
        DischargeReason.ContractNotRenewed,
        DischargeReason.CommanderDead,
        DischargeReason.CommanderUnavailableGraceExpired,
    };

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _contentStore = new EnlistmentContentStore(_logger);
        _rewards = Substitute.For<IServiceRewardService>();
        _ledger = new PersistedEquipmentIssueLedger(_contentStore);
        _partyItems = Substitute.For<IPartyItemRosterAdapter>();
        _feature = Substitute.For<IEnlistmentFeatureSettingsProvider>();
        _inquiry = Substitute.For<IInquiryAdapter>();

        // Defaults: healthy party, feature on, every removal succeeds. Individual tests
        // break exactly one link.
        _partyItems.IsMainPartyAvailable().Returns(true);
        _partyItems.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(1);
        _feature.IsEnabled.Returns(true);

        _service = new DischargeConsequenceService(
            _contentStore, _rewards, _ledger, _partyItems, _feature, _inquiry, _logger);
    }

    private void IssueKit(params string[] itemIds)
    {
        _ledger.RecordIssue(EnlistmentRank.Recruit, itemIds);
    }

    // ---------------------------------------------------------------- arrears

    [TestMethod]
    public void ApplyConsequences_Desertion_DoesNotSettleArrears()
    {
        _contentStore.Record.DeferredWages = 120;

        _service.ApplyConsequences(DischargeReason.Desertion);

        // No final settlement is the forfeit: the arrears are zeroed and never paid out. The
        // record itself cannot witness it — Clear() zeroes every field on every reason.
        _rewards.DidNotReceive().Grant(Arg.Any<RewardSpec>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyConsequences_HonourableReason_SettlesDeferredWages()
    {
        foreach (var reason in HonourableReasons)
        {
            Setup();
            _contentStore.Record.DeferredWages = 120;

            _service.ApplyConsequences(reason);

            _rewards.Received(1).Grant(
                Arg.Is<RewardSpec>(r => r.Gold == 120), "final-settlement");
        }
    }

    [TestMethod]
    public void ApplyConsequences_QuietBookkeepingReason_DoesNotSettleArrears()
    {
        foreach (var reason in new[]
                 {
                     DischargeReason.HeirSuccessionOrPossessionMismatch,
                     DischargeReason.SaveNormalization,
                 })
        {
            Setup();
            _contentStore.Record.DeferredWages = 120;

            _service.ApplyConsequences(reason);

            _rewards.DidNotReceive().Grant(Arg.Any<RewardSpec>(), Arg.Any<string>());
        }
    }

    // ------------------------------------------------------------ kit reclaim

    [TestMethod]
    public void ApplyConsequences_HonourableReason_RemovesOnePerLedgerEntry()
    {
        foreach (var reason in HonourableReasons)
        {
            Setup();
            IssueKit("helm_a", "chest_a", "boots_a");

            _service.ApplyConsequences(reason);

            _partyItems.Received(1).RemoveItem("helm_a", 1);
            _partyItems.Received(1).RemoveItem("chest_a", 1);
            _partyItems.Received(1).RemoveItem("boots_a", 1);
        }
    }

    [TestMethod]
    public void ApplyConsequences_DuplicateIssuedIds_RemovesOncePerInstance()
    {
        IssueKit("javelin_a", "javelin_a", "javelin_a");

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.Received(3).RemoveItem("javelin_a", 1);
    }

    [TestMethod]
    public void ApplyConsequences_Desertion_LeavesTheKitWithThePlayer()
    {
        IssueKit("helm_a", "chest_a");

        _service.ApplyConsequences(DischargeReason.Desertion);

        _partyItems.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyConsequences_QuietBookkeepingReason_LeavesTheKitAlone()
    {
        foreach (var reason in new[]
                 {
                     DischargeReason.HeirSuccessionOrPossessionMismatch,
                     DischargeReason.SaveNormalization,
                 })
        {
            Setup();
            IssueKit("helm_a", "chest_a");

            _service.ApplyConsequences(reason);

            _partyItems.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        }
    }

    [TestMethod]
    public void ApplyConsequences_EmptyLedger_RemovesNothingAndSaysNothing()
    {
        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _inquiry.DidNotReceive().ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyConsequences_LedgerEntryIsEmptyString_IsSkipped()
    {
        // A hand-edited save can carry a blank id; the roster adapter would reject it anyway,
        // but skipping keeps the reported count honest.
        _contentStore.Record.IssuedEquipmentItemIds.Add(string.Empty);
        _contentStore.Record.IssuedEquipmentItemIds.Add("helm_a");

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.DidNotReceive().RemoveItem(string.Empty, Arg.Any<int>());
        _partyItems.Received(1).RemoveItem("helm_a", 1);
    }

    // --------------------------------------- trust the return value, not the ledger

    [TestMethod]
    public void ApplyConsequences_PlayerSoldPartOfTheKit_ReportsOnlyWhatWasRemoved()
    {
        // The ledger says three; only one is still in the unmodified stack.
        IssueKit("helm_a", "chest_a", "boots_a");
        _partyItems.RemoveItem("helm_a", 1).Returns(1);
        _partyItems.RemoveItem("chest_a", 1).Returns(0);
        _partyItems.RemoveItem("boots_a", 1).Returns(0);

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_kit_reclaimed", Arg.Any<string>(), "COUNT", "1");
    }

    [TestMethod]
    public void ApplyConsequences_PlayerModifiedTheWholeKit_RemovesNothing_ShowsNoMessage()
    {
        // RemoveItem drains only the UNMODIFIED stack, so a "Sharp"/"Battered" variant the
        // player earned is out of reach by design and returns 0 — never report a phantom take.
        IssueKit("helm_a", "chest_a");
        _partyItems.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(0);

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _inquiry.DidNotReceive().ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyConsequences_KitFullyReclaimed_ReportsTheFullCount()
    {
        IssueKit("helm_a", "chest_a", "boots_a");

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_kit_reclaimed", Arg.Any<string>(), "COUNT", "3");
    }

    // ------------------------------------------------------------------ guards

    [TestMethod]
    public void ApplyConsequences_MainPartyUnavailable_RemovesNothing()
    {
        IssueKit("helm_a", "chest_a");
        _partyItems.IsMainPartyAvailable().Returns(false);

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _inquiry.DidNotReceive().ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyConsequences_FeatureSwitchedOff_KeepsTheKitPerTheSettingsHint()
    {
        // TaomSettings.EnableEnlistment's shipped hint promises that turning the feature OFF
        // releases you "honourably (you keep your pay and your gear)". EnlistmentReconciler
        // runs that release as PlayerRequest, indistinguishable here from a normal one — so
        // the toggle is what has to be checked, not the reason.
        IssueKit("helm_a", "chest_a");
        _feature.IsEnabled.Returns(false);

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyConsequences_FeatureSwitchedOff_StillSettlesDeferredWages()
    {
        // Same hint, other half: "you keep your PAY and your gear".
        _contentStore.Record.DeferredWages = 90;
        _feature.IsEnabled.Returns(false);

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Gold == 90), "final-settlement");
    }

    // -------------------------------------------------------------- ordering

    [TestMethod]
    public void ApplyConsequences_HonourableReason_ReclaimsBeforeClearingTheRecord()
    {
        // Clear() empties IssuedEquipmentItemIds, so a reclaim ordered after it would silently
        // remove nothing forever.
        IssueKit("helm_a", "chest_a");

        _service.ApplyConsequences(DischargeReason.PlayerRequest);

        _partyItems.Received(1).RemoveItem("helm_a", 1);
        Assert.AreEqual(0, _contentStore.Record.IssuedEquipmentItemIds.Count,
            "the ledger must be empty once the record is cleared");
    }

    [TestMethod]
    public void ApplyConsequences_EveryReason_ClearsTheContentRecord()
    {
        // INV: whatever the consequence policy does, the content record always ends clean —
        // the discharge pipeline has already cleared the core record by this point.
        foreach (DischargeReason reason in Enum.GetValues(typeof(DischargeReason)))
        {
            Setup();
            _contentStore.Record.DeferredWages = 40;
            _contentStore.Record.ServiceXp = 500;
            IssueKit("helm_a");

            _service.ApplyConsequences(reason);

            Assert.AreEqual(0, _contentStore.Record.DeferredWages, $"({reason}) arrears");
            Assert.AreEqual(0, _contentStore.Record.ServiceXp, $"({reason}) xp");
            Assert.AreEqual(0, _contentStore.Record.IssuedEquipmentItemIds.Count, $"({reason}) kit ledger");
            Assert.IsNull(_contentStore.Record.HighestIssuedEquipmentRank, $"({reason}) kit rank");
        }
    }

    // -------------------------------------------------------------- wiring seam

    [TestMethod]
    public void DischargeConsequenceService_Resolvable_AcrossTheEnlistmentAndDutiesModules()
    {
        // The reclaim reports through IInquiryAdapter, which EnlistmentIoC does NOT register —
        // DutiesIoC does, on the next line of Main/IoC.cs, into the same container. That seam is
        // invisible to every other unit test in this file, and a wiring failure here is exactly
        // the class of bug that ships green (see EnlistmentContainerWiringTests' header).
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        EnlistmentIoC.RegisterEnlistmentFeature(container);
        global::TAOM.Features.Enlistment.Duties.DutiesIoC.RegisterEnlistmentDutiesFeature(container);

        var errors = container.Validate(typeof(IDischargeConsequenceService));

        Assert.AreEqual(
            0,
            errors.Length,
            "IDischargeConsequenceService is not resolvable: "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
