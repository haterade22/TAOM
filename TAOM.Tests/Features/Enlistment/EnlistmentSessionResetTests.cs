using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.Enlistment.Presentation;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Session scope for the Enlistment singletons (csharp-architecture.md, "Singleton Services Holding
/// Per-Campaign State MUST Have a Session-Reset Story"). Every service here is Reuse.Singleton and
/// outlives the campaign. Three kinds of per-session value are covered. The dwell anchor and the
/// offer cooldown hold absolute campaign hours: loading an earlier save, or starting a new
/// campaign, runs the clock backwards, and a stamp left in the future reads as "a moment ago" until
/// the new clock catches up with it. The rhythm snapshot is keyed on an equal hour stamp, so its
/// hazard is a reload inside the same campaign hour serving the previous world's snapshot. The
/// adapter's cached commander MobileParty is a handle matched by StringId, which a later campaign
/// can reissue. The hook tests pin that both lifecycle edges reach the one reset point.
/// </summary>
[TestClass]
public class EnlistmentSessionResetTests
{
    // ---- settlement-dwell anchor (ServiceAttachmentService) ------------------------------------

    private static ServiceAttachmentService NewAttachment() =>
        NewAttachment(Substitute.For<IMobilePartyAttachmentAdapter>());

    private static ServiceAttachmentService NewAttachment(IMobilePartyAttachmentAdapter adapter) =>
        new ServiceAttachmentService(adapter, Substitute.For<IGameMenuAdapter>(), Substitute.For<IModLogger>());

    [TestMethod]
    public void AttachmentReset_AlsoDropsTheAdaptersCachedCommanderParty()
    {
        // One session-reset member on the attachment service: the cached commander MobileParty is
        // matched by StringId, which a later campaign can reissue.
        var adapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        var sut = NewAttachment(adapter);

        sut.ResetForNewSession();

        adapter.Received(1).InvalidateCommanderCache();
    }

    [TestMethod]
    public void AttachmentReset_DropsTheDwellAnchor_SoAnEarlierClockIsNotInsideTheDwell()
    {
        var sut = NewAttachment();
        sut.StampSettlementEntry(1000.0);
        Assert.IsTrue(sut.IsWithinSettlementDwell(10.0),
            "Precondition: an anchor in the future reads as inside the dwell; that is what the reset exists to clear.");

        sut.ResetForNewSession();

        Assert.IsFalse(sut.IsWithinSettlementDwell(10.0));
        Assert.IsFalse(sut.IsWithinSettlementDwell(1001.0));
    }

    [TestMethod]
    public void AttachmentReset_ThenANewPlacement_StartsAFreshDwell()
    {
        var sut = NewAttachment();
        sut.StampSettlementEntry(1000.0);
        sut.ResetForNewSession();

        sut.StampSettlementEntry(10.0);

        Assert.IsTrue(sut.IsWithinSettlementDwell(12.0));
        Assert.IsFalse(sut.IsWithinSettlementDwell(16.0));
    }

    // ---- arrival-offer latch (EnlistmentWaitMenuPresenter) --------------------------------------

    private static EnlistmentWaitMenuPresenter NewPresenter(IInquiryAdapter inquiry)
    {
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord());
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var actions = Substitute.For<IEnlistmentPlayerActionService>();
        actions.CanTakeTownLeave().Returns(true);
        var settings = Substitute.For<IEnlistmentFeatureSettingsProvider>();
        settings.IsEnabled.Returns(true);
        settings.OfferLeaveOnArrival.Returns(true);

        return new EnlistmentWaitMenuPresenter(store, Substitute.For<ICommanderLordAdapter>(),
            Substitute.For<IEnlistmentDialogGateService>(), Substitute.For<IEnlistmentService>(),
            inquiry, coop, actions, Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IServiceStatusService>(), settings, Substitute.For<IModLogger>());
    }

    private static void AssertOffersShown(IInquiryAdapter inquiry, int count) =>
        inquiry.Received(count).ShowTwoOptionInquiry(
            "taom_enlist_arrival_title", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Action>(), Arg.Any<System.Action>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());

    [TestMethod]
    public void PresenterReset_ReArmsTheArrivalOffer_OnAnEarlierClock()
    {
        var inquiry = Substitute.For<IInquiryAdapter>();
        var sut = NewPresenter(inquiry);
        sut.OfferTownLeave("town_EW1", 1000.0);
        sut.OfferTownLeave("town_EW2", 10.0);   // an earlier save: the clock ran backwards
        AssertOffersShown(inquiry, 1);          // precondition: suppressed without a reset

        sut.ResetForNewSession();
        sut.OfferTownLeave("town_EW2", 10.0);

        AssertOffersShown(inquiry, 2);
    }

    [TestMethod]
    public void PresenterReset_ReArmsTheArrivalOffer_ForTheSameSettlement()
    {
        // The settlement-id latch is session state too: past the cooldown, only the reset re-arms
        // an offer for the town the previous session last offered.
        var inquiry = Substitute.For<IInquiryAdapter>();
        var sut = NewPresenter(inquiry);
        sut.OfferTownLeave("town_EW1", 1000.0);

        sut.ResetForNewSession();
        sut.OfferTownLeave("town_EW1", 1030.0);

        AssertOffersShown(inquiry, 2);
    }

    // ---- army-rhythm snapshot (ArmyRhythmSnapshotService) --------------------------------------

    [TestMethod]
    public void RhythmReset_ForcesAFreshProbe_InTheSameCampaignHour()
    {
        // A quick reload inside the same campaign hour would otherwise serve the pre-load world.
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord());
        var content = Substitute.For<IEnlistmentContentStore>();
        content.Record.Returns(new ServiceContentRecord());
        var probe = Substitute.For<IArmyRhythmProbeAdapter>();
        probe.Probe(Arg.Any<string>()).Returns(new ArmyRhythmProbe());
        var sut = new ArmyRhythmSnapshotService(store, content, probe);

        sut.GetSnapshot(10.0, 12.0);
        sut.GetSnapshot(10.0, 12.0);
        probe.Received(1).Probe(Arg.Any<string>());

        sut.ResetForNewSession();
        sut.GetSnapshot(10.0, 12.0);

        probe.Received(2).Probe(Arg.Any<string>());
    }

    // ---- the lifecycle hooks (EnlistmentBehavior) ----------------------------------------------

    private static EnlistmentBehavior NewBehavior(
        IEnlistmentStore store, IServiceMaintenanceService maintenance,
        ICoopSessionProvider? coop = null, IPlayerPartyAdapter? playerParty = null,
        IEnlistmentLoadNormalizer? normalizer = null) =>
        new EnlistmentBehavior(store, Substitute.For<IEnlistmentStateMachine>(),
            Substitute.For<IEnlistmentReconciler>(), normalizer ?? Substitute.For<IEnlistmentLoadNormalizer>(),
            playerParty ?? Substitute.For<IPlayerPartyAdapter>(), coop ?? Substitute.For<ICoopSessionProvider>(),
            maintenance, Substitute.For<IModLogger>());

    [TestMethod]
    public void NewCampaign_DropsTheSessionCaches_AndStillClearsTheStore()
    {
        // The engine fires OnNewGameCreated, never OnGameLoaded, for a new campaign
        // (Campaign.DoLoadingForGameType), so the load hook's reset alone left campaign two
        // running on campaign one's caches. The co-op substitute defaults IsAuthority to false:
        // this reset is deliberately not authority-gated (it only nulls in-memory fields).
        var store = Substitute.For<IEnlistmentStore>();
        var maintenance = Substitute.For<IServiceMaintenanceService>();
        var sut = NewBehavior(store, maintenance);

        sut.OnNewGameCreated(null!);

        maintenance.Received(1).ResetSessionCaches();
        store.Received(1).Clear();
    }

    [TestMethod]
    public void NewCampaign_AfterALoadingSyncData_StillResets_ButKeepsTheLoadedRecord()
    {
        // The reset is not gated on _justLoadedFromSave; only the store clear is.
        var store = Substitute.For<IEnlistmentStore>();
        var maintenance = Substitute.For<IServiceMaintenanceService>();
        var sut = NewBehavior(store, maintenance);
        var dataStore = Substitute.For<TaleWorlds.CampaignSystem.IDataStore>();
        dataStore.IsSaving.Returns(false);
        sut.SyncData(dataStore);

        sut.OnNewGameCreated(null!);

        maintenance.Received(1).ResetSessionCaches();
        store.DidNotReceive().Clear();
    }

    private sealed class ReachedNormalizeArguments : System.Exception { }

    [TestMethod]
    public void GameLoad_OnTheHost_ResetsTheSessionCaches_BeforeNormalizing()
    {
        // OnGameLoaded cannot finish outside a campaign (CampaignTime.Now). The hero-id argument
        // is evaluated before CampaignTime.Now, so a sentinel thrown there stops the hook after
        // the reset and before the engine read, which pins the load edge's routing and order.
        var store = Substitute.For<IEnlistmentStore>();
        var maintenance = Substitute.For<IServiceMaintenanceService>();
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var playerParty = Substitute.For<IPlayerPartyAdapter>();
        playerParty.GetMainHeroId().Returns(_ => throw new ReachedNormalizeArguments());
        var normalizer = Substitute.For<IEnlistmentLoadNormalizer>();
        var sut = NewBehavior(store, maintenance, coop, playerParty, normalizer);

        Assert.ThrowsException<ReachedNormalizeArguments>(() => sut.OnGameLoaded(null!));

        Received.InOrder(() =>
        {
            maintenance.ResetSessionCaches();
            playerParty.GetMainHeroId();
        });
        normalizer.DidNotReceiveWithAnyArgs().Normalize(default!, default);
    }
}
