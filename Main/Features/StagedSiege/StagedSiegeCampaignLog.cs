using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// What became of the settlement after a staged battle (docs/features/staged-siege.md "Run report", #1054). The walls and hall
/// services record the battle's settlement id in <see cref="StagedSiegeSlot"/> when they shut down in a campaign; this behavior
/// listens to the map event end and the settlement owner change and logs, for that settlement only, who won and what each side lost,
/// and who owns it afterwards. It clears the record after the map event end line. Read-only log lines: no state, no save, and nothing
/// to gate in co-op (the staged siege is absent there, so the record is never set). Built with <c>new</c> by
/// <see cref="StagedSiegeModule"/> for every campaign start, so its constructor is the per-campaign reset. Each handler catches: a
/// campaign event that throws takes the game down. The engine-facing handlers only pull plain values out and call the internal
/// <c>Handle</c> methods, which carry the filter.
///
/// Order of events (v1.5.6 decompile): <c>MapEvent.FinalizeEventAux</c> raises <c>OnMapEventEnded</c> (MapEvent.cs:1737) and only then
/// <c>Component.OnBeforeMapEventFinalize()</c> (:1738). For a siege assault that (SiegeAssaultEventComponent.cs:237-253) raises
/// <c>SiegeCompleted</c> on an attacker victory, <c>KingdomManager.SiegeCompleted</c> (KingdomManager.cs:241-260) calls
/// <c>ChangeOwnerOfSettlementAction.ApplyBySiege</c>, which raises <c>OnSettlementOwnerChanged</c>
/// (ChangeOwnerOfSettlementAction.cs:94). Every step is a synchronous call, so the owner change of a captured settlement arrives after
/// the map event end, at the same campaign time. The record is cleared at the map event end, so the behavior also remembers the
/// settlement and that time, and logs an owner change that matches both; any later change of it is not logged.
/// </summary>
internal sealed class StagedSiegeCampaignLog : CampaignBehaviorBase
{
    private readonly IModLogger _logger;
    private string? _endedSettlement;
    private double _endedAt;

    public StagedSiegeCampaignLog(IModLogger logger)
    {
        _logger = logger;
        StagedSiegeSlot.ClearCampaignSettlement();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
    }

    /// <summary>Nothing is saved: the record lives for one battle, and a loaded save starts with none.</summary>
    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        try
        {
            HandleMapEventEnded(mapEvent?.MapEventSettlement?.StringId, mapEvent == null ? "None" : mapEvent.WinningSide.ToString(),
                mapEvent?.AttackerSide?.TroopCasualties ?? 0, mapEvent?.DefenderSide?.TroopCasualties ?? 0, CampaignTime.Now.ToMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{StagedSiegeService.Tag} campaign log failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        try
        {
            HandleOwnerChanged(settlement?.StringId, oldOwner?.Clan?.StringId ?? "none", newOwner?.Clan?.StringId ?? "none", detail.ToString(),
                CampaignTime.Now.ToMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{StagedSiegeService.Tag} campaign log failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal void HandleMapEventEnded(string? settlementId, string winner, int attackerCasualties, int defenderCasualties, double campaignMilliseconds)
    {
        try
        {
            if (!IsRecorded(settlementId))
                return;
            var side = string.Equals(winner, "None", StringComparison.Ordinal) ? "none" : winner;
            _logger.LogInfo($"{StagedSiegeService.Tag} campaign: map event at {settlementId} ended, winner {side}, "
                + $"casualties attacker {attackerCasualties} defender {defenderCasualties}");
            StagedSiegeSlot.ClearCampaignSettlement();
            _endedSettlement = settlementId;
            _endedAt = campaignMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{StagedSiegeService.Tag} campaign log failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal void HandleOwnerChanged(string? settlementId, string oldClanId, string newClanId, string detail, double campaignMilliseconds)
    {
        try
        {
            var sameChain = !string.IsNullOrEmpty(settlementId) && string.Equals(settlementId, _endedSettlement, StringComparison.Ordinal)
                && campaignMilliseconds == _endedAt;
            if (!IsRecorded(settlementId) && !sameChain)
                return;
            if (sameChain)
                _endedSettlement = null;
            _logger.LogInfo($"{StagedSiegeService.Tag} campaign: {settlementId} owner {oldClanId} -> {newClanId} ({detail})");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{StagedSiegeService.Tag} campaign log failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsRecorded(string? settlementId) =>
        !string.IsNullOrEmpty(settlementId) && string.Equals(settlementId, StagedSiegeSlot.CampaignSettlementId, StringComparison.Ordinal);
}
