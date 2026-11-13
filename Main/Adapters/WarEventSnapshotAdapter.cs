using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
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
    private readonly IPlayerControlQuery _playerControl;

    public WarEventSnapshotAdapter(
        IPlayerContextAdapter playerContext,
        IModLogger logger,
        IPlayerControlQuery playerControl)
    {
        _playerContext = playerContext;
        _logger = logger;
        _playerControl = playerControl;
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
                                 || IsPlayerRelated(defenderParty, playerKingdomId)
                                 || HasPlayerParty(mapEvent.AttackerSide)
                                 || HasPlayerParty(mapEvent.DefenderSide),
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
                PlayerInvolved = IsPlayerRelated(capturerParty?.Party, _playerContext.GetPlayerKingdomId())
                                 || PlayerFought(capturerParty),
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

    // #1015: the capturer is the leader of the side that won (AttackerSide.LeaderParty after an assault,
    // DefenderSide.LeaderParty after a failed sally-out), so credit follows the player's own party being
    // on that side, whatever it is led by. The sides are still populated here: SiegeCompleted is raised
    // before MapEvent.FinalizeEventAux detaches the parties (v1.5.5 MapEvent.cs:1738 vs :1743).
    internal bool PlayerFought(MobileParty capturerParty)
        => HasPlayerParty(capturerParty?.Party?.MapEventSide);

    public RaidOutcomeSnapshot FromRaid(BattleSideEnum winnerSide, RaidEventComponent component)
    {
        try
        {
            var attackerParty = component?.AttackerSide?.LeaderParty;
            var faction = attackerParty?.MapFaction;
            return new RaidOutcomeSnapshot
            {
                AttackerVictory = winnerSide == BattleSideEnum.Attacker,
                // #774: the engine sets the village to Looted just before raising RaidCompleted,
                // and only when its hit points reached zero (v1.5.6 RaidEventComponent.cs:269-277).
                VillageLooted = component?.MapEventSettlement?.Village?.VillageState
                                == Village.VillageStates.Looted,
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
    //
    // Under co-op the world meter is shared, so a remote player's party and a kingdom holding any
    // player's clan count as the player's side too. Without a session OfParty and OfClan reduce to the
    // local-player comparison, so singleplayer is unchanged.
    private bool IsPlayerRelated(PartyBase party, string playerKingdomId)
    {
        var mobile = party?.MobileParty;
        var sameKingdom = mobile?.Owner != null
                          && ((!string.IsNullOrEmpty(playerKingdomId) && party.MapFaction?.StringId == playerKingdomId)
                              || KingdomHoldsPlayerClan(party.MapFaction));
        return IsPlayerRelated(mobile?.IsMainParty == true, _playerControl.OfParty(mobile), sameKingdom);
    }

    // Unknown (registry unreadable) is NOT involved: a guess would let a stranger's wars move the meter.
    internal static bool IsPlayerRelated(bool isMainParty, PlayerControl partyControl, bool sameKingdom)
        => isMainParty || partyControl == PlayerControl.Player || sameKingdom;

    private bool KingdomHoldsPlayerClan(IFaction faction)
    {
        if (faction is not Kingdom kingdom)
            return false;

        foreach (var clan in kingdom.Clans)
        {
            if (_playerControl.OfClan(clan) == PlayerControl.Player)
                return true;
        }
        return false;
    }

    private bool HasPlayerParty(MapEventSide side)
    {
        var parties = side?.Parties;
        if (parties == null)
            return false;

        foreach (var mapEventParty in parties)
        {
            if (_playerControl.OfParty(mapEventParty?.Party?.MobileParty) == PlayerControl.Player)
                return true;
        }
        return false;
    }

    private static string LeaderName(PartyBase party)
    {
        return party?.MobileParty?.Owner?.Name?.ToString() ?? party?.Name?.ToString() ?? "";
    }
}
