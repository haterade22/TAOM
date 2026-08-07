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
                default:
                    _logger?.LogWarning($"[Enlistment] '{partyId}' is in a map event but on side {party.Party.Side}");
                    return null;
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

    public EncounterOwnershipSnapshot GetOwnership(string commanderPartyId)
    {
        // Each field is read in its OWN try. A wholesale catch here would report "nothing live"
        // on any single throw, and the oath would then stop closing the encounter it genuinely
        // owns — silently reintroducing the bug that made the player unable to interact at all.
        var readFailed = false;

        bool hasEncounter;
        try { hasEncounter = PlayerEncounter.Current != null; }
        catch (Exception ex) { readFailed = true; hasEncounter = false; _logger?.LogError($"[Enlistment] GetOwnership: PlayerEncounter.Current threw: {ex.Message}"); }

        if (!hasEncounter)
            return new EncounterOwnershipSnapshot(hasEncounter: false, readFailed: readFailed);

        bool conversation;
        try { conversation = Campaign.Current?.ConversationManager?.IsConversationInProgress == true; }
        catch (Exception ex) { readFailed = true; conversation = false; _logger?.LogError($"[Enlistment] GetOwnership: conversation read threw: {ex.Message}"); }

        MobileParty encountered = null;
        try { encountered = PlayerEncounter.EncounteredMobileParty; }
        catch (Exception ex) { readFailed = true; _logger?.LogError($"[Enlistment] GetOwnership: EncounteredMobileParty threw: {ex.Message}"); }

        bool playerInMapEvent;
        try { playerInMapEvent = PartyBase.MainParty?.MapEvent != null; }
        catch (Exception ex) { readFailed = true; playerInMapEvent = false; _logger?.LogError($"[Enlistment] GetOwnership: MainParty.MapEvent threw: {ex.Message}"); }

        bool insideSettlement;
        try { insideSettlement = MobileParty.MainParty?.CurrentSettlement != null; }
        catch (Exception ex) { readFailed = true; insideSettlement = false; _logger?.LogError($"[Enlistment] GetOwnership: CurrentSettlement threw: {ex.Message}"); }

        var encounteredId = encountered?.StringId;
        var related = !string.IsNullOrEmpty(encounteredId)
            && !string.IsNullOrEmpty(commanderPartyId)
            && string.Equals(encounteredId, commanderPartyId, StringComparison.Ordinal);

        return new EncounterOwnershipSnapshot(
            hasEncounter: true,
            conversationInProgress: conversation,
            hasEncounteredMobileParty: encountered != null,
            encounteredPartyId: encounteredId,
            encounteredPartyIsCommanderRelated: related,
            playerInMapEvent: playerInMapEvent,
            playerInsideSettlement: insideSettlement,
            readFailed: readFailed);
    }

    public bool IsMainPartyInMapEvent
    {
        get
        {
            try
            {
                return PartyBase.MainParty?.MapEvent != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[Enlistment] IsMainPartyInMapEvent failed: {ex.Message}");
                return false;
            }
        }
    }

    // Mechanical joinability only — see IEncounterAdapter for why MapEvent.CanPartyJoinBattle
    // must NOT be reinstated here.
    public bool IsCommanderBattleJoinable(string partyId, PartyBattleSide side)
    {
        try
        {
            var mapEvent = FindParty(partyId)?.MapEvent;
            if (mapEvent == null)
            {
                _logger?.LogInfo($"[Enlistment] '{partyId}' is not in a map event — nothing to join");
                return false;
            }

            if (mapEvent.IsFinalized)
            {
                _logger?.LogInfo($"[Enlistment] map event of '{partyId}' is already finalized — too late to join");
                return false;
            }

            if (mapEvent.GetMapEventSide(ToEngineSide(side)) == null)
            {
                _logger?.LogWarning($"[Enlistment] map event of '{partyId}' has no {side} side");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] IsCommanderBattleJoinable('{partyId}', {side}) failed: {ex.Message}");
            return false;
        }
    }

    public bool EnsureEncounterAgainst(string partyId)
    {
        try
        {
            var mapEvent = FindParty(partyId)?.MapEvent;
            if (mapEvent == null)
                return false;

            if (PlayerEncounter.Current != null && PlayerEncounter.EncounteredBattle == mapEvent)
                return true;

            // Do NOT force-clear blind. A live encounter that is not the commander's belongs to
            // the player; the caller owns the ownership decision, so refuse and say why rather
            // than tearing down their business to make room for ours.
            if (PlayerEncounter.Current != null)
            {
                _logger?.LogWarning($"[Enlistment] cannot seed an encounter for '{partyId}': one is already live and is not this battle. Refusing to clear it blind.");
                return false;
            }

            // Side LEADER parties, not the MapEventStarted arguments: leaders are PartyBase, so a
            // besieged settlement resolves. Going through MobileParty ids drops sieges silently.
            //
            // PRECONDITION (verified against 1.4.7): passing two FOREIGN parties here — vanilla
            // always passes MainParty as one of them — is only safe because
            // MobileParty.MainParty.AttachedTo is null. PlayerEncounter.SetupFields picks
            // _encounteredParty by excluding MainParty and MainParty.AttachedTo, so with
            // AttachedTo set it could resolve to neither leader and EncounteredBattle would not
            // match this event. Enlistment tracks the player via IsActive/IsVisible/position sync
            // and ParkNear explicitly clears AttachedTo — if that ever changes, revisit this.
            var attackerLeader = mapEvent.AttackerSide?.LeaderParty;
            var defenderLeader = mapEvent.DefenderSide?.LeaderParty;
            if (attackerLeader == null || defenderLeader == null)
            {
                _logger?.LogWarning($"[Enlistment] map event of '{partyId}' has no resolvable side leaders");
                return false;
            }

            PlayerEncounter.RestartPlayerEncounter(defenderLeader, attackerLeader, forcePlayerOutFromSettlement: false);

            var seeded = PlayerEncounter.Current != null && PlayerEncounter.EncounteredBattle == mapEvent;
            if (!seeded)
                _logger?.LogWarning($"[Enlistment] encounter seeded for '{partyId}' but EncounteredBattle is not its map event");
            return seeded;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] EnsureEncounterAgainst('{partyId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool JoinBattle(PartyBattleSide side)
    {
        try
        {
            PlayerEncounter.JoinBattle(ToEngineSide(side));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] JoinBattle({side}) threw: {ex.Message}");
            return false;
        }

        // A non-throwing call proves nothing: JoinBattleInternal silently calls Finish() and
        // returns when EncounteredBattle is null. Verify the party actually landed in the event,
        // otherwise the caller would leave state at EnlistedBattle with the player outside the
        // fight — which then blocks every subsequent battle.
        var joined = IsMainPartyInMapEvent;
        if (!joined)
            _logger?.LogWarning($"[Enlistment] JoinBattle({side}) did not put the main party into a map event");
        return joined;
    }

    public bool LeaveSettlementIfUnderSiege()
    {
        try
        {
            // The InsideSettlement check is load-bearing, not defensive: LeaveSettlement() calls
            // LeaveSettlementAction.ApplyForParty, which dereferences MainParty.CurrentSettlement
            // unguarded and NREs when the party is not in a settlement. Do not reorder or drop it.
            if (PlayerEncounter.Current == null || !PlayerEncounter.InsideSettlement)
                return false;

            var settlement = PlayerEncounter.EncounterSettlement;
            if (settlement == null || !settlement.IsUnderSiege)
                return false;

            PlayerEncounter.LeaveSettlement();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] LeaveSettlementIfUnderSiege failed: {ex.Message}");
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
