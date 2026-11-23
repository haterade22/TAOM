using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FiefGranting;
using TAOM.Features.FiefGranting.Hooks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #565: the participation record lives in a process-lifetime singleton and SyncData only runs when
/// a save record exists, so a fresh campaign, or a save from before the feature, MUST reset the
/// service or it inherits (and then saves) the previous campaign's record. The reset runs in the
/// constructor (#773), the WarOfTheRingBehavior shape; SubModule's per-campaign construction is
/// pinned in <c>PerCampaignBehaviorWiringTests</c>. The two event handlers are exercised through
/// reflection with uninitialized engine objects, which is enough to pin their gates: neither
/// handler may touch the record for a null settlement, a non-siege battle, or a co-op client.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FiefGrantingBehaviorSessionResetTests
{
    private IFiefSiegeParticipationService _participation = null!;
    private ICoopSessionProvider _coop = null!;
    private FiefGrantingCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _participation = Substitute.For<IFiefSiegeParticipationService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _sut = new FiefGrantingCampaignBehavior(_participation, _coop, Substitute.For<IModLogger>());
    }

    private void SyncWith(bool isLoading)
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(isLoading);
        _sut.SyncData(dataStore);
    }

    private void Invoke(string method, params object?[] args)
    {
        var target = typeof(FiefGrantingCampaignBehavior).GetMethod(
            method, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(target, method + " must be a private instance method");
        target!.Invoke(_sut, args);
    }

    // ---------------------------------------------------------------- session reset

    [TestMethod]
    public void Constructor_FreshCampaign_ResetsTheService()
    {
        // #773: the behavior is built in every campaign's OnGameStart, before any campaign event and
        // before a load's SyncData, so the reset sits in its constructor.
        _participation.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_RestoresTheRecordOverTheConstructorReset()
    {
        SyncWith(isLoading: true);

        // A substitute IDataStore leaves the ref null, which is exactly a pre-#565 save: the
        // service must be told to start empty, not left holding the previous session's record.
        Received.InOrder(() =>
        {
            _participation.ResetForNewSession();
            _participation.RestoreFromSave(null);
        });
    }

    [TestMethod]
    public void SavingSyncData_SnapshotsTheRecord_AndNeitherResetsAgainNorRestores()
    {
        _participation.SnapshotForSave().Returns(new Dictionary<string, string>());

        SyncWith(isLoading: false);

        _participation.Received(1).SnapshotForSave();
        _participation.Received(1).ResetForNewSession();
        _participation.DidNotReceive().RestoreFromSave(Arg.Any<Dictionary<string, string>>());
    }

    // ---------------------------------------------------------------- owner-change gate

    private static Settlement BareSettlement() =>
        (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

    private static object?[] OwnerChangedArgs(
        Settlement? settlement, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail) =>
        new object?[] { settlement, false, null, null, null, detail };

    [TestMethod]
    public void OnSettlementOwnerChanged_BySiegeThatOpensNoClaim_ForgetsTheRecord()
    {
        // Codex F2 (#565): a bare settlement is not a fortification and the new owner here has no
        // faction, so vanilla would never open a claim for this capture. No claim means no grant
        // and no ByKingDecision to clear the record, so the behavior must drop it itself. The keep
        // half (a multi-clan kingdom taking a fortification) is pinned on WillOpenAClaim directly
        // in FiefGrantingBehaviorCaptureGateTests.
        Invoke("OnSettlementOwnerChanged", OwnerChangedArgs(
            BareSettlement(), ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege));

        _participation.Received(1).Forget(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByClanDestruction)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)]
    [DataRow(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.Default)]
    public void OnSettlementOwnerChanged_AnyNonSiegeTransfer_ForgetsTheRecord(
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        // An uninitialized Settlement carries a null StringId; the call reaching Forget at all is
        // what this pins, and Forget itself treats null as a no-op.
        Invoke("OnSettlementOwnerChanged", OwnerChangedArgs(BareSettlement(), detail));

        _participation.Received(1).Forget(Arg.Any<string>());
    }

    [TestMethod]
    public void OnSettlementOwnerChanged_OnACoopClient_LeavesTheRecordAlone()
    {
        _coop.IsAuthority.Returns(false);

        Invoke("OnSettlementOwnerChanged", OwnerChangedArgs(
            BareSettlement(), ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision));

        _participation.DidNotReceive().Forget(Arg.Any<string>());
    }

    [TestMethod]
    public void OnSettlementOwnerChanged_NullSettlement_DoesNothing()
    {
        Invoke("OnSettlementOwnerChanged", OwnerChangedArgs(
            null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision));

        _participation.DidNotReceive().Forget(Arg.Any<string>());
    }

    // ---------------------------------------------------------------- map-event gate

    [TestMethod]
    public void OnMapEventEnded_NullEvent_RecordsNothing()
    {
        Invoke("OnMapEventEnded", new object?[] { null });

        _participation.DidNotReceive().RecordAssault(Arg.Any<string>(), Arg.Any<IReadOnlyList<KeyValuePair<string, int>>>());
    }

    [TestMethod]
    public void OnMapEventEnded_ABattleThatIsNotASiege_RecordsNothing()
    {
        // An uninitialized MapEvent has BattleTypes.None, so none of the four siege flags is set.
        var fieldBattle = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));

        Invoke("OnMapEventEnded", new object?[] { fieldBattle });

        _participation.DidNotReceive().RecordAssault(Arg.Any<string>(), Arg.Any<IReadOnlyList<KeyValuePair<string, int>>>());
    }

    [TestMethod]
    public void OnMapEventEnded_OnACoopClient_RecordsNothing()
    {
        _coop.IsAuthority.Returns(false);
        var anyEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));

        Invoke("OnMapEventEnded", new object?[] { anyEvent });

        _participation.DidNotReceive().RecordAssault(Arg.Any<string>(), Arg.Any<IReadOnlyList<KeyValuePair<string, int>>>());
    }
}
