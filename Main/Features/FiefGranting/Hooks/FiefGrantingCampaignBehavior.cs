using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.FiefGranting.Hooks;

/// <summary>
/// Campaign entry point for the siege participation record (#565): writes who fought when a siege
/// battle ends in a capture that will open a claim, forgets it on any other owner change, owns the
/// SyncData halves. The record is the singleton <see cref="IFiefSiegeParticipationService"/>; the
/// engine-derived rules (which battles capture, which clans may claim, when vanilla opens a claim)
/// live in <see cref="FiefSiegeCaptureRules"/>. Ordering: <c>MapEventEnded</c> precedes the
/// <c>BySiege</c> owner change in the same finalize (v1.4.8 MapEvent.cs:2079 against :2093), so the
/// record exists before the keep/forget decision runs. Both handlers gate on <c>IsAuthority</c>: a
/// co-op client keeps no record and takes the host's through the save. Full argument in
/// docs/features/fief-granting.md ("The participation record").
/// </summary>
public class FiefGrantingCampaignBehavior : CampaignBehaviorBase
{
    private const string SaveKey = "_taomFiefSiegeParticipation";

    private readonly IFiefSiegeParticipationService _participation;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    /// <summary>True once SyncData ran in LOADING mode this session; a fresh campaign and a pre-#565 save start empty.</summary>
    private bool _syncedThisSession;

    public FiefGrantingCampaignBehavior(
        IFiefSiegeParticipationService participation, ICoopSessionProvider coop, IModLogger logger)
    {
        _participation = participation;
        _coop = coop;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Direction-split: loading starts from a null local so a missing key yields an empty record.
        if (dataStore.IsLoading)
        {
            Dictionary<string, string> snapshot = null;
            dataStore.SyncData(SaveKey, ref snapshot);
            _participation.RestoreFromSave(snapshot);
            _syncedThisSession = true;
        }
        else
        {
            var snapshot = _participation.SnapshotForSave();
            dataStore.SyncData(SaveKey, ref snapshot);
        }
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => ResetIfNoLoadedRecord();

    private void OnGameLoaded(CampaignGameStarter starter) => ResetIfNoLoadedRecord();

    /// <summary>When no save record loaded this session, the process-lifetime service still holds the
    /// PREVIOUS campaign's record. Latches so the two callers cannot double-reset. Internal for tests.</summary>
    internal bool ResetIfNoLoadedRecord()
    {
        if (_syncedThisSession)
            return false;
        _participation.ResetForNewSession();
        _syncedThisSession = true;
        return true;
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        try
        {
            if (mapEvent == null || !_coop.IsAuthority) return;

            var side = FiefSiegeCaptureRules.SideThatCapturesOnVictory(mapEvent);
            if (side == BattleSideEnum.None || mapEvent.WinningSide != side) return;

            var settlement = mapEvent.MapEventSettlement;
            if (settlement == null) return;

            // The faction that receives the settlement: KingdomManager.SiegeCompleted reads it off
            // the same leader party.
            var capturer = mapEvent.GetMapEventSide(side)?.LeaderParty?.MobileParty?.MapFaction;

            var contributions = new List<KeyValuePair<string, int>>();
            foreach (var party in mapEvent.PartiesOnSide(side))
            {
                var clan = FiefSiegeCaptureRules.ClanOf(party);
                if (!FiefSiegeCaptureRules.CanClaimFief(clan, capturer)) continue;
                contributions.Add(new KeyValuePair<string, int>(clan.StringId, party.ContributionToBattle));
            }

            _participation.RecordAssault(settlement.StringId, contributions);

            if (_participation.HasRecord(settlement.StringId))
                _logger.LogInfo(
                    $"[FiefGrant] {settlement.StringId} stormed: {contributions.Count} winning " +
                    "parties on record for the next fief election.");
        }
        catch (Exception ex)
        {
            // A fief record must never be the reason a battle fails to end.
            _logger.LogWarning(
                $"[FiefGrant] could not record the assault ({ex.GetType().Name}: {ex.Message}); " +
                "the next election for this settlement runs without a participation record.");
        }
    }

    private void OnSettlementOwnerChanged(
        Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (settlement == null || !_coop.IsAuthority) return;

        // A siege wrote its record moments ago in the same finalize; keep it only when the grant
        // that will clear it is actually coming. Any other transfer means nobody fought this time.
        if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege
            && FiefSiegeCaptureRules.WillOpenAClaim(newOwner?.MapFaction, settlement.IsFortification))
            return;

        _participation.Forget(settlement.StringId);
    }
}
