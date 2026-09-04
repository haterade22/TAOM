using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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

        // Read BOTH, because they clear together at the end of the aftermath and either one being
        // set means the battle is not finished with this encounter yet. `PlayerEncounter.Battle`
        // is the encounter's own `_mapEvent`; `IsJoinedBattle` is set in `JoinBattleInternal` and
        // reset in the same block that nulls `_mapEvent`. Deliberately not `EncounteredBattle`,
        // which dereferences `Current._encounteredParty` with no null guard.
        bool isBattleEncounter;
        try { isBattleEncounter = PlayerEncounter.Battle != null || PlayerEncounter.Current?.IsJoinedBattle == true; }
        catch (Exception ex) { readFailed = true; isBattleEncounter = false; _logger?.LogError($"[Enlistment] GetOwnership: battle-encounter read threw: {ex.Message}"); }

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

    /// <summary>
    /// Vanilla's own recipe, lifted from
    /// <c>EncounterGameMenuBehavior.game_menu_siege_attacker_left_return_to_settlement_on_consequence</c>
    /// (installed v1.4.8): establish the encounter, then the location encounter, then let the
    /// caller open the menu.
    ///
    /// <c>PlayerEncounter.Start()</c> + <c>SetupFields</c> rather than
    /// <c>EncounterManager.StartSettlementEncounter</c>, for two reasons that are both engine
    /// facts rather than preference. <c>PlayerEncounter.Init</c> is <c>internal</c>, so
    /// <c>StartSettlementEncounter</c> is the only way to reach it, and it unconditionally calls
    /// <c>EnterSettlement()</c> for a settlement defender. On a party that is ALREADY inside (the
    /// enlistment follow path put it there) that re-runs <c>EnterSettlementAction.ApplyForParty</c>,
    /// which has no already-inside guard and re-dispatches
    /// <c>OnBeforeSettlementEntered</c>/<c>OnSettlementEntered</c>/<c>OnAfterSettlementEntered</c>.
    /// The duty runtime treats <c>OnSettlementEntered</c> as a completion trigger. <c>SetupFields</c>
    /// is public, sets <c>EncounterSettlementAux</c> from a settlement defender party, and TAOM
    /// already uses it this way in <c>MessengerCampaignBehavior</c>.
    /// </summary>
    public bool EnsureSettlementEncounter(string settlementId)
    {
        // Declared OUTSIDE the try so the catch can roll back. Set ONLY on the branch that found
        // Current null, so a PRE-EXISTING foreign encounter is never destroyed by our failure.
        var createdHere = false;
        try
        {
            var main = MobileParty.MainParty;
            // Settlement.Find, NOT CampaignObjectManager.Find<Settlement> — the latter returns null
            // unconditionally (no Settlement type is registered), the same trap documented on
            // MobilePartyAttachmentAdapter.MoveIntoSettlement.
            var settlement = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
            if (main == null || settlement == null)
            {
                _logger?.LogError($"[Enlistment] EnsureSettlementEncounter('{settlementId}') — no main party or no such settlement");
                return false;
            }

            // KIND CHECK BEFORE Start(), not after. CreateLocationEncounter knows four kinds; for
            // anything else it returns null and the verify below fails — but by then we would have
            // created a PlayerEncounter we cannot complete, and returning false while leaving one
            // live is the save-breaker this feature already alarms on. Refuse before we allocate.
            if (!settlement.IsTown && !settlement.IsVillage && !settlement.IsCastle && !settlement.IsHideout)
            {
                _logger?.LogError($"[Enlistment] EnsureSettlementEncounter('{settlementId}') — none of town/castle/village/hideout, so no LocationEncounter kind covers it");
                return false;
            }

            if (PlayerEncounter.Current == null)
            {
                PlayerEncounter.Start();
                createdHere = true;
                // Mirror PlayerEncounter.Init (installed v1.4.8): Init assigns this and SetupFields
                // does not, and DefaultBattleRewardModel.GetPlayerGainedRelationAmount reads it if
                // the visit ever turns hostile. Without it that reward is computed from zero.
                PlayerEncounter.Current.PlayerPartyInitialStrength = PartyBase.MainParty.CalculateCurrentStrength();
                PlayerEncounter.Current.SetupFields(PartyBase.MainParty, settlement.Party);
            }
            else if (PlayerEncounter.EncounterSettlement != settlement)
            {
                // Someone else's encounter, against something other than this settlement. Do not
                // repoint it — the caller's failure branch walks the player back out, which is far
                // safer than handing them a menu whose encounter describes a different place.
                _logger?.LogError(
                    $"[Enlistment] EnsureSettlementEncounter('{settlementId}') — a live encounter already points at " +
                    $"'{PlayerEncounter.EncounterSettlement?.StringId ?? "nothing"}'; refusing to repoint it");
                return false;
            }

            if (PlayerEncounter.LocationEncounter == null)
            {
                if (main.CurrentSettlement == settlement)
                    PlayerEncounter.LocationEncounter = CreateLocationEncounter(settlement);
                else
                    PlayerEncounter.EnterSettlement();
            }

            // VERIFY, never assume — and verify the location encounter points at THIS settlement.
            // A non-null one for somewhere else is skipped by the null check above and would
            // otherwise pass here, which is the dangerous direction: returning true is what makes
            // both callers open a vanilla menu.
            var ok = main.CurrentSettlement == settlement
                && PlayerEncounter.Current != null
                && PlayerEncounter.LocationEncounter?.Settlement == settlement;

            if (!ok)
            {
                _logger?.LogError(
                    $"[Enlistment] EnsureSettlementEncounter('{settlementId}') did not land: " +
                    $"inside={main.CurrentSettlement?.StringId ?? "-"} encounter={PlayerEncounter.Current != null} " +
                    $"location={PlayerEncounter.LocationEncounter?.Settlement?.StringId ?? "-"}");
                RollBackIfCreatedHere(createdHere, settlementId);
            }

            return ok;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] EnsureSettlementEncounter('{settlementId}') failed: {ex.Message}");
            RollBackIfCreatedHere(createdHere, settlementId);
            return false;
        }
    }

    /// <summary>
    /// A false return must mean NOTHING was left behind. <c>PlayerEncounter.Finish</c> is the only
    /// public route to clearing <c>Campaign.Current.PlayerEncounter</c> (the setter is internal),
    /// and it nulls both that and <c>LocationEncounter</c> on every path. Gated on
    /// <paramref name="createdHere"/> so a pre-existing foreign encounter is untouchable.
    ///
    /// <c>forcePlayerOutFromSettlement: false</c> — we may have moved the party in, but walking it
    /// back out is the caller's decision, and both callers already handle their own placement.
    /// </summary>
    private void RollBackIfCreatedHere(bool createdHere, string settlementId)
    {
        if (!createdHere)
            return;
        try
        {
            PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] EnsureSettlementEncounter('{settlementId}') rollback ALSO failed: {ex.Message} — a PlayerEncounter may be stranded");
        }
    }

    /// <summary>
    /// Mirrors vanilla's private <c>PlayerEncounter.CreateLocationEncounter</c>. Null for anything
    /// that is none of the four kinds, which the caller treats as a failure rather than a menu.
    /// </summary>
    private static LocationEncounter CreateLocationEncounter(Settlement settlement)
    {
        if (settlement.IsTown) return new TownEncounter(settlement);
        if (settlement.IsVillage) return new VillageEncounter(settlement);
        if (settlement.IsCastle) return new CastleEncounter(settlement);
        if (settlement.IsHideout) return new HideoutEncounter(settlement);
        return null;
    }

    // NO DEFAULT, deliberately. The engine's own default is TRUE; this once defaulted to false,
    // the exact inverted polarity that let call sites silently skip LeaveSettlement(). Every
    // caller states its intent.
    public bool Finish(bool forcePlayerOutFromSettlement)
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
