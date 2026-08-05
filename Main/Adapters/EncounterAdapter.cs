using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class EncounterAdapter : IEncounterAdapter
{
    private readonly IModLogger _logger;

    public EncounterAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool HasCurrent => PlayerEncounter.Current != null;

    public bool IsInsideSettlement => PlayerEncounter.InsideSettlement;

    public string EncounteredPartyId => PlayerEncounter.EncounteredMobileParty?.StringId;

    public PartyBattleSide? GetPartyBattleSide(string partyId)
    {
        try
        {
            var party = FindParty(partyId);
            if (party?.MapEvent == null)
                return null;

            switch (party.Party.Side)
            {
                case BattleSideEnum.Defender: return PartyBattleSide.Defender;
                case BattleSideEnum.Attacker: return PartyBattleSide.Attacker;
                default: return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPartyBattleSide('{partyId}') failed: {ex.Message}");
            return null;
        }
    }

    public bool IsPartyInMapEvent(string partyId)
    {
        try
        {
            return FindParty(partyId)?.MapEvent != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] IsPartyInMapEvent('{partyId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool CanMainPartyJoinBattleOf(string partyId, PartyBattleSide side)
    {
        try
        {
            var mapEvent = FindParty(partyId)?.MapEvent;
            if (mapEvent == null)
                return false;
            return mapEvent.CanPartyJoinBattle(PartyBase.MainParty, ToEngineSide(side));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CanMainPartyJoinBattleOf('{partyId}', {side}) failed: {ex.Message}");
            return false;
        }
    }

    public bool JoinBattle(PartyBattleSide side)
    {
        try
        {
            PlayerEncounter.JoinBattle(ToEngineSide(side));
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] JoinBattle({side}) failed: {ex.Message}");
            return false;
        }
    }

    public bool RestartBattle(string defenderPartyId, string attackerPartyId)
    {
        try
        {
            var defender = FindParty(defenderPartyId);
            var attacker = FindParty(attackerPartyId);
            if (defender == null || attacker == null)
                return false;

            PlayerEncounter.RestartPlayerEncounter(defender.Party, attacker.Party, forcePlayerOutFromSettlement: false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] RestartBattle('{defenderPartyId}', '{attackerPartyId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool Finish(bool forcePlayerOutFromSettlement = false)
    {
        try
        {
            if (PlayerEncounter.Current == null)
                return true;
            PlayerEncounter.Finish(forcePlayerOutFromSettlement);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] Finish failed: {ex.Message}");
            return false;
        }
    }

    private static MobileParty FindParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<MobileParty>(partyId);
    }

    private static BattleSideEnum ToEngineSide(PartyBattleSide side) =>
        side == PartyBattleSide.Attacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
}
