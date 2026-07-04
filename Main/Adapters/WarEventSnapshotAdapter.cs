using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum.Snapshots;

namespace TAOM.Adapters;

/// <summary>
/// Sig notes (installed 1.4.6, verified via taom-src): MapEventSide.TroopCasualties
/// (rename of Casualties); MapEventSide.MapFaction is a computed getter that derefs
/// LeaderParty — factions resolve via LeaderParty?.MapFaction instead (adapters.md);
/// RaidEventComponent members forward to base.MapEvent (computed) — whole method is
/// exception-guarded; the RaidCompleted BattleSideEnum arg IS the winner side
/// (RaidEventComponent.cs:138).
/// </summary>
public class WarEventSnapshotAdapter : IWarEventSnapshotAdapter
{
    private readonly IPlayerContextAdapter _playerContext;
    private readonly IModLogger _logger;

    public WarEventSnapshotAdapter(IPlayerContextAdapter playerContext, IModLogger logger)
    {
        _playerContext = playerContext;
        _logger = logger;
    }

    public BattleOutcomeSnapshot FromMapEvent(MapEvent mapEvent)
    {
        if (mapEvent == null)
            return null;

        try
        {
            var attackerParty = mapEvent.AttackerSide?.LeaderParty;
            var defenderParty = mapEvent.DefenderSide?.LeaderParty;
            var attackerFaction = attackerParty?.MapFaction;
            var defenderFaction = defenderParty?.MapFaction;
            var winner = mapEvent.Winner; // null unless Attacker/DefenderVictory
            var playerKingdomId = _playerContext.GetPlayerKingdomId();

            return new BattleOutcomeSnapshot
            {
                HasWinner = winner != null,
                AttackerWon = winner != null && winner == mapEvent.AttackerSide,
                AttackerFactionId = attackerFaction?.StringId,
                DefenderFactionId = defenderFaction?.StringId,
                AttackerIsKingdomFaction = attackerFaction?.IsKingdomFaction == true,
                DefenderIsKingdomFaction = defenderFaction?.IsKingdomFaction == true,
                DefenderIsMobileParty = defenderParty?.IsMobile == true,
                AttackerCasualties = mapEvent.AttackerSide?.TroopCasualties ?? 0,
                DefenderCasualties = mapEvent.DefenderSide?.TroopCasualties ?? 0,
                IsValidBattleType = mapEvent.EventType == MapEvent.BattleTypes.FieldBattle
                                    || mapEvent.EventType == MapEvent.BattleTypes.Raid
                                    || mapEvent.EventType == MapEvent.BattleTypes.SiegeOutside,
                PlayerInvolved = mapEvent.IsPlayerMapEvent
                                 || IsPlayerRelated(attackerParty, playerKingdomId)
                                 || IsPlayerRelated(defenderParty, playerKingdomId),
                AttackerFactionName = attackerFaction?.Name?.ToString() ?? "",
                DefenderFactionName = defenderFaction?.Name?.ToString() ?? "",
                AttackerLeaderName = LeaderName(attackerParty),
                DefenderLeaderName = LeaderName(defenderParty),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"WarEventSnapshotAdapter: failed to snapshot MapEvent: {ex.Message}");
            return null;
        }
    }

    public SiegeOutcomeSnapshot FromSiege(Settlement settlement, MobileParty capturerParty)
    {
        try
        {
            var faction = capturerParty?.MapFaction;
            return new SiegeOutcomeSnapshot
            {
                CaptorFactionId = faction?.StringId,
                PlayerInvolved = capturerParty?.IsMainParty == true,
                CaptorFactionName = faction?.Name?.ToString() ?? "",
                CaptorLeaderName = capturerParty?.LeaderHero?.Name?.ToString() ?? "",
                SettlementName = settlement?.Name?.ToString() ?? "",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"WarEventSnapshotAdapter: failed to snapshot siege: {ex.Message}");
            return null;
        }
    }

    public RaidOutcomeSnapshot FromRaid(BattleSideEnum winnerSide, RaidEventComponent component)
    {
        try
        {
            var attackerParty = component?.AttackerSide?.LeaderParty;
            var faction = attackerParty?.MapFaction;
            return new RaidOutcomeSnapshot
            {
                AttackerVictory = winnerSide == BattleSideEnum.Attacker,
                AttackerFactionId = faction?.StringId,
                AttackerPartyName = attackerParty?.Name?.ToString() ?? "",
                AttackerFactionName = faction?.Name?.ToString() ?? "",
                SettlementName = component?.MapEventSettlement?.Name?.ToString() ?? "",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"WarEventSnapshotAdapter: failed to snapshot raid: {ex.Message}");
            return null;
        }
    }

    public ArmyGatheredSnapshot FromArmyGathered(Army army)
    {
        try
        {
            return new ArmyGatheredSnapshot
            {
                KingdomId = army?.Kingdom?.StringId,
                ArmyLeaderName = army?.ArmyOwner?.Name?.ToString() ?? "",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"WarEventSnapshotAdapter: failed to snapshot army: {ex.Message}");
            return null;
        }
    }

    // PartyBase.Owner is a BANNED throwing computed getter (PartyOwnerGetterBanTests,
    // crash 0b462fd8) — the owner hero resolves via MobileParty?.Owner instead, which
    // also matches LOTRAOM's semantics (its leader parties are mobile parties).
    private static bool IsPlayerRelated(PartyBase party, string playerKingdomId)
    {
        if (party?.MobileParty?.IsMainParty == true)
            return true;

        return party?.MobileParty?.Owner != null
               && !string.IsNullOrEmpty(playerKingdomId)
               && party.MapFaction?.StringId == playerKingdomId;
    }

    private static string LeaderName(PartyBase party)
    {
        return party?.MobileParty?.Owner?.Name?.ToString() ?? party?.Name?.ToString() ?? "";
    }
}
