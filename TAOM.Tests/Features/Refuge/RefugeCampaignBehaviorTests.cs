using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Components;
using TAOM.Features.Refuge.Domain;
using TAOM.Features.Refuge.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The session-reset pattern on <see cref="RefugeCampaignBehavior"/> (the
/// FieldCampBehaviorSessionResetTests shape): SyncData marks the session synced ONLY when the
/// store is loading; OnGameLoaded and the session-launch gate reset the process-lifetime service
/// for a fresh campaign AND for a save with no refuge record. Without it, campaign B inherits
/// campaign A's book from the singleton and then saves it as its own. The gate latches, so the
/// two callers cannot double-wipe a book founded right after launch.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RefugeCampaignBehaviorTests
{
    private IRefugeService _refuges = null!;
    private RefugeCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _refuges = Substitute.For<IRefugeService>();
        _sut = new RefugeCampaignBehavior(
            _refuges,
            Substitute.For<IRefugeSettingsProvider>(),
            Substitute.For<IRefugeVisualService>(),
            Substitute.For<IWardenService>(),
            Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IEncounterAdapter>(),
            Substitute.For<IModLogger>());
    }

    private void SyncWith(bool isLoading)
    {
        var dataStore = Substitute.For<IDataStore>();
        dataStore.IsLoading.Returns(isLoading);
        _sut.SyncData(dataStore);
    }

    private void InvokeOnGameLoaded()
    {
        var method = typeof(RefugeCampaignBehavior).GetMethod(
            "OnGameLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "OnGameLoaded must be a private instance method");
        method!.Invoke(_sut, new object[] { null! });
    }

    // --- The reset gate ---

    [TestMethod]
    public void FreshCampaign_NoSyncData_ResetsTheService()
    {
        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());

        _refuges.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void LoadingSyncData_MarksTheSessionSynced_NoReset()
    {
        SyncWith(isLoading: true);

        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());
        _refuges.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void SavingSyncData_DoesNotCountAsSynced()
    {
        // Only the LOADING direction proves this session's book came from this save; a save pass
        // over a stale book must not launder it into "synced".
        SyncWith(isLoading: false);

        Assert.IsTrue(_sut.ResetIfNoLoadedRecord());
        _refuges.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void ResetLatches_SecondCallerCannotWipeThePostLaunchBook()
    {
        _sut.ResetIfNoLoadedRecord();
        Assert.IsFalse(_sut.ResetIfNoLoadedRecord());

        _refuges.Received(1).ResetForNewSession();
    }

    // --- OnGameLoaded routing ---

    [TestMethod]
    public void OnGameLoaded_AfterLoadingSync_RunsTheServicePostLoadRepair()
    {
        SyncWith(isLoading: true);

        InvokeOnGameLoaded();

        _refuges.Received(1).OnGameLoaded();
        _refuges.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void OnGameLoaded_PreFeatureSave_ResetsInsteadOfReconcilingStaleState()
    {
        // No SyncData ran (the save predates the feature): the singleton still holds the
        // previous session's book; reconciling it would adopt/re-show stale state.
        InvokeOnGameLoaded();

        _refuges.Received(1).ResetForNewSession();
        _refuges.DidNotReceive().OnGameLoaded();
    }

    // --- SyncData plumbing (direction-split: save never replays the load-time transient wipe) ---

    [TestMethod]
    public void SyncData_Saving_WritesTheBookWithoutReloadingIt()
    {
        // The old symmetric shape ran LoadFrom on every SAVE, wiping transients (hold-note
        // dedupe, frame-work clock) mid-session on each autosave (round B; Codex round 2 #8).
        SyncWith(isLoading: false);

        _refuges.Received(1).SaveInto(out Arg.Any<Dictionary<string, RefugeData>>(), out Arg.Any<int>());
        _refuges.DidNotReceive().LoadFrom(Arg.Any<Dictionary<string, RefugeData>>(), Arg.Any<int>());
    }

    [TestMethod]
    public void SyncData_Loading_StartsFromNulledLocals_NeverPreSeedsTheLiveBook()
    {
        // The store substitute leaves the ref untouched, modeling a record whose key is MISSING:
        // the service must receive null (-> empty book), never the live singleton book the old
        // shape pre-seeded (which silently kept the previous session's state, Codex round 2 #2).
        SyncWith(isLoading: true);

        _refuges.Received(1).LoadFrom(null, 0);
        _refuges.DidNotReceive().SaveInto(out Arg.Any<Dictionary<string, RefugeData>>(), out Arg.Any<int>());
    }

    // --- OnTick: unconditional frame work (the CampService split) ---

    [TestMethod]
    public void OnTick_MasterToggleOff_StillPumpsFrameTick()
    {
        // Gating FrameTick on Enabled froze a mid-build refuge into an unreachable state
        // (round B MED) and stopped the post-load visual rebuild + wind (round B LOW). The
        // gameplay half (hold-nearby) gates INSIDE the service; the pump is unconditional.
        // Setup's settings substitute defaults Enabled to false.
        var onTick = typeof(RefugeCampaignBehavior).GetMethod(
            "OnTick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(onTick);
        onTick!.Invoke(_sut, new object[] { 0.016f });

        _refuges.Received(1).FrameTick();
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }

    // --- The map-event listeners given a null MapEvent ---

    private void InvokeHandler(string name, params object?[] args)
    {
        var method = typeof(RefugeCampaignBehavior).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, name + " must be a private instance method");
        method!.Invoke(_sut, args);
    }

    [TestMethod]
    public void OnMapEventStarted_NoRefuge_NullEvent_ReadsTheBookOnceAndDispatchesNothing()
    {
        _refuges.AllRefuges.Returns(new List<RefugeData>());

        InvokeHandler("OnMapEventStarted", null, null, null);

        _ = _refuges.Received(1).AllRefuges;
        _refuges.DidNotReceiveWithAnyArgs().OnMapEventStarted(default!);
    }

    [TestMethod]
    public void OnMapEventEnded_NoRefuge_NullEvent_ReadsTheBookOnceAndDispatchesNothing()
    {
        _refuges.AllRefuges.Returns(new List<RefugeData>());

        InvokeHandler("OnMapEventEnded", new object?[] { null });

        _ = _refuges.Received(1).AllRefuges;
        _refuges.DidNotReceiveWithAnyArgs().OnMapEventEnded(default!);
    }

    [TestMethod]
    public void OnMapEventStarted_WithARefuge_NullEvent_RalliesNothing()
    {
        _refuges.AllRefuges.Returns(new List<RefugeData> { new RefugeData { PartyId = "refuge_1" } });

        InvokeHandler("OnMapEventStarted", null, null, null);

        _refuges.DidNotReceiveWithAnyArgs().OnMapEventStarted(default!);
    }

    // --- The same listeners on a real battle: the guard is neither inverted nor dropped ---
    //
    // A null MapEvent walks to no party whichever way the guard points, so the null-event tests above cannot
    // tell the guard from its inverse (Codex review of plan 037). These stage a battle with a refuge party in
    // it, from bare engine objects as FiefGrantingBehaviorCaptureGateTests does: the walk is
    // MapEvent.InvolvedParties over _sides and each side's party list. With a refuge in the book the listener
    // must pass that party's id on; with none it must dispatch nothing for the same battle. That is all these
    // can see of the guard: that the book is read before the walk is pinned in IL, as call order and not
    // control flow, by MapEventListeners_ReadTheBookBeforeWalkingTheBattle_InIlOrder, below.

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetProperty(object target, string name, object? value)
    {
        var setter = target.GetType().GetProperty(name)?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{name} lost its setter; the test cannot stage the battle.");
        setter!.Invoke(target, new[] { value });
    }

    private static void SetField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone; the test cannot stage the battle.");
        field!.SetValue(target, value);
    }

    private static MobileParty LordParty(string id)
    {
        var party = Bare<MobileParty>();
        party.StringId = id;
        return party;
    }

    private static MobileParty RefugeParty(string id)
    {
        var party = LordParty(id);
        SetField(party, "_partyComponent", Bare<RefugePartyComponent>());
        return party;
    }

    /// <summary>A battle with these parties on one side. A null entry is a party with no MobileParty, as a
    /// settlement's is.</summary>
    private static MapEvent BattleOf(params MobileParty?[] parties)
    {
        var members = new MBList<MapEventParty>();
        foreach (var mobile in parties)
        {
            var partyBase = Bare<PartyBase>();
            SetProperty(partyBase, nameof(PartyBase.MobileParty), mobile);
            var member = Bare<MapEventParty>();
            SetProperty(member, nameof(MapEventParty.Party), partyBase);
            members.Add(member);
        }
        var side = Bare<MapEventSide>();
        SetField(side, "_battleParties", members);
        var battle = Bare<MapEvent>();
        SetField(battle, "_sides", new[] { side });
        return battle;
    }

    private void HoldOneRefuge() =>
        _refuges.AllRefuges.Returns(new List<RefugeData> { new RefugeData { PartyId = "refuge_1" } });

    [TestMethod]
    public void OnMapEventStarted_WithARefuge_RalliesOnlyTheRefugePartiesInTheBattle()
    {
        HoldOneRefuge();
        var battle = BattleOf(LordParty("lord_1"), null, RefugeParty("refuge_1"));

        InvokeHandler("OnMapEventStarted", battle, null, null);

        _refuges.Received(1).OnMapEventStarted("refuge_1");
        _refuges.ReceivedWithAnyArgs(1).OnMapEventStarted(default!);
    }

    [TestMethod]
    public void OnMapEventEnded_WithARefuge_StandsDownOnlyTheRefugePartiesInTheBattle()
    {
        HoldOneRefuge();
        var battle = BattleOf(LordParty("lord_1"), null, RefugeParty("refuge_1"));

        InvokeHandler("OnMapEventEnded", battle);

        _refuges.Received(1).OnMapEventEnded("refuge_1");
        _refuges.ReceivedWithAnyArgs(1).OnMapEventEnded(default!);
    }

    [TestMethod]
    public void OnMapEventStarted_NoRefuge_DispatchesNothingForABattleWithARefugeParty()
    {
        _refuges.AllRefuges.Returns(new List<RefugeData>());

        InvokeHandler("OnMapEventStarted", BattleOf(LordParty("lord_1"), null, RefugeParty("refuge_1")), null, null);

        _refuges.DidNotReceiveWithAnyArgs().OnMapEventStarted(default!);
    }

    [TestMethod]
    public void OnMapEventEnded_NoRefuge_DispatchesNothingForABattleWithARefugeParty()
    {
        _refuges.AllRefuges.Returns(new List<RefugeData>());

        InvokeHandler("OnMapEventEnded", BattleOf(LordParty("lord_1"), null, RefugeParty("refuge_1")));

        _refuges.DidNotReceiveWithAnyArgs().OnMapEventEnded(default!);
    }

    // --- The same listeners in IL: the book is read before the battle is walked ---
    //
    // The walk cannot be seen from the test side. RefugePartyIds swallows everything it throws, and each step of
    // it reads a plain member that reports nothing (MapEvent._sides, MapEventSide.Parties, MapEventParty.Party,
    // PartyBase.MobileParty, MobileParty.PartyComponent). So the order is pinned in the IL, as the desertion gate
    // is in AlignmentDesertionBehaviorTests: the call order of a straight-line method, not control flow. Moving
    // the guard below the walk keeps every outcome above (an empty book still dispatches nothing) and brings
    // back the per-battle walk that plan 037 removed, so only this test notices it.
    [DataTestMethod]
    [DataRow("OnMapEventStarted")]
    [DataRow("OnMapEventEnded")]
    public void MapEventListeners_ReadTheBookBeforeWalkingTheBattle_InIlOrder(string listener)
    {
        var method = typeof(RefugeCampaignBehavior).GetMethod(listener, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, listener + " must be a private instance method");

        var calls = IlCallScanner.ExtractCalledMethods(method!, method!.GetMethodBody()!.GetILAsByteArray()!).ToList();
        int book = calls.FindIndex(m => m.Name == "get_AllRefuges" && m.DeclaringType == typeof(IRefugeService));
        int walk = calls.FindIndex(m => m.Name == "RefugePartyIds");

        Assert.IsTrue(book >= 0, "IRefugeService.AllRefuges is read");
        Assert.IsTrue(walk >= 0, "RefugePartyIds is called");
        Assert.IsTrue(book < walk, $"AllRefuges (call #{book}) comes before RefugePartyIds (call #{walk})");
    }
}
