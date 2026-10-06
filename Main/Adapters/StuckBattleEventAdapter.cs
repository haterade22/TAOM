using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;

namespace TAOM.Adapters;

/// <inheritdoc cref="IStuckBattleEventAdapter"/>
internal sealed class StuckBattleEventAdapter : IStuckBattleEventAdapter
{
    private readonly MapEvent _mapEvent;
    private readonly IModLogger _logger;

    internal StuckBattleEventAdapter(MapEvent mapEvent, IModLogger logger)
    {
        _mapEvent = mapEvent;
        _logger = logger;
    }

    public StuckBattleSnapshot? Read()
    {
        try
        {
            if (_mapEvent.IsFinalized)
                return null;

            var partyIds = new List<string>();
            int destroyed = 0;
            foreach (var party in _mapEvent.InvolvedParties)
            {
                if (party == null)
                    continue;
                partyIds.Add(party.Id);
                if (IsWreckToDetach(party))
                    destroyed++;
            }

            return new StuckBattleSnapshot(
                ageHours: _mapEvent.BattleStartTime.ElapsedHoursUntilNow,
                attackerHealthy: _mapEvent.AttackerSide.RecalculateMemberCountOfSide(),
                defenderHealthy: _mapEvent.DefenderSide.RecalculateMemberCountOfSide(),
                destroyedParties: destroyed,
                isVillageHostileAction: _mapEvent.IsRaid || _mapEvent.IsForcingSupplies || _mapEvent.IsForcingVolunteers,
                involvesPlayer: InvolvesThePlayer(),
                hasPendingOutcome: _mapEvent.DiplomaticallyFinished || _mapEvent.BattleState != BattleState.None,
                partyIds: partyIds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[StuckBattle] could not read a map event: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    public string Label()
    {
        try
        {
            string place = _mapEvent.MapEventSettlement?.Name?.ToString() ?? string.Empty;
            string sides = $"{SideLabel(_mapEvent.AttackerSide)} vs {SideLabel(_mapEvent.DefenderSide)}";
            return place.Length == 0 ? sides : $"{sides} at {place}";
        }
        catch (Exception ex)
        {
            return $"a map event ({ex.GetType().Name} reading its names)";
        }
    }

    public int DetachWrecks()
    {
        // Listed first: each write shrinks InvolvedParties, and the last party of a side finalizes the event.
        var wrecks = _mapEvent.InvolvedParties.Where(p => p != null && IsWreckToDetach(p)).ToArray();
        int count = 0;
        while (count < wrecks.Length && !_mapEvent.IsFinalized)
            wrecks[count++].MapEventSide = null;
        return count;
    }

    public void AwardVictory(bool attackerWins) =>
        _mapEvent.SetOverrideWinner(attackerWins ? BattleSideEnum.Attacker : BattleSideEnum.Defender);

    // A wreck: a mobile party the engine no longer counts as active. The player's own party is skipped (it is
    // inactive while it follows an enlisted commander), and so is a quest's, which vanilla's DestroyPartyAction
    // leaves attached on purpose.
    private static bool IsWreckToDetach(PartyBase party)
    {
        var mobile = party.MobileParty;
        return mobile != null && party != PartyBase.MainParty && !party.IsActive && !mobile.IsCurrentlyUsedByAQuest;
    }

    // PlayerEncounter.EncounteredBattle, rebuilt null-safe: its settlement branch reads the besieger camp's leader
    // unguarded, and that leader can be null while besiegers remain. A leaderless camp has no encountered battle.
    private bool InvolvesThePlayer()
    {
        if (_mapEvent.IsPlayerMapEvent || PlayerEncounter.Battle == _mapEvent)
            return true;
        var encountered = PlayerEncounter.EncounteredParty;
        if (encountered == null)
            return false;
        var battle = encountered.MapEvent
            ?? (encountered.IsSettlement ? encountered.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapEvent : null);
        return battle == _mapEvent;
    }

    private static string SideLabel(MapEventSide side)
    {
        var leader = side?.LeaderParty;
        string name = leader?.Name?.ToString() ?? "nobody";
        string? faction = leader?.MapFaction?.Name?.ToString();
        return string.IsNullOrEmpty(faction) ? name : $"{name} ({faction})";
    }
}
