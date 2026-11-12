using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Runs vanilla's own private casualty-loot step for a siege assault that ends by attacker retreat (#1121). At the
/// target's prefix the map event already stands at <c>DefenderVictory</c>: the mission end builds the result with
/// <c>MissionResult.CreateSuccessful</c> and <c>CampaignMissionComponent.OnMissionResultReady</c> calls
/// <c>PlayerEncounter.SetPlayerVictorious</c>. The step's own checks (<c>IsPlayerMapEvent</c>, the winning side) then
/// hold, so the adapter marks nothing and restores nothing; vanilla's next line clears the winner and the besiegers stay.
/// The private target is pinned by <c>Patch146SiegeRetreatLootBindingTests</c> and the reflection-sites catalogue.
/// </summary>
internal sealed class SiegeRetreatLootAdapter : ISiegeRetreatLootAdapter
{
    private static readonly MethodInfo? CasualtiesStep = AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties");

    private readonly IModLogger _logger;

    public SiegeRetreatLootAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool CanLootCasualties()
    {
        try
        {
            var mapEvent = MapEvent.PlayerMapEvent;
            if (mapEvent == null || PlayerEncounter.Current == null || PlayerSiege.PlayerSiegeEvent == null)
                return false;

            // The condition of vanilla's own retreat branch: the player's opponent is the besieging side.
            if (mapEvent.GetMapEventSide(mapEvent.PlayerSide).OtherSide != mapEvent.AttackerSide)
                return false;

            if (mapEvent.BattleState != BattleState.DefenderVictory)
                return false;

            return mapEvent.Component != null && mapEvent.Component.CanLootDefeatedPartyCasualties();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeRetreatLoot] could not read the siege assault, the retreat gives no loot as in vanilla: {ex.Message}");
            return false;
        }
    }

    public bool RunCasualtyLoot()
    {
        var mapEvent = MapEvent.PlayerMapEvent;
        if (mapEvent == null || CasualtiesStep == null)
            return false;

        try
        {
            // The same lists CalculateAndCommitMapEventResults passes (MapEvent.cs:1202-1206).
            var winners = new MBList<MapEventParty>(mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties);
            var defeated = new MBList<MapEventParty>(mapEvent.GetMapEventSide(mapEvent.DefeatedSide).Parties);
            CasualtiesStep.Invoke(mapEvent, new object[] { winners, defeated });
        }
        catch (Exception ex)
        {
            // The step may have moved items before it threw: the screen still shows them.
            _logger.LogError($"[SiegeRetreatLoot] the loot step threw, the retreat gives the loot collected so far: {ex}");
        }

        return true;
    }

    public int OpenLootScreen()
    {
        try
        {
            var encounter = PlayerEncounter.Current;
            if (encounter == null)
            {
                _logger.LogError("[SiegeRetreatLoot] could not open the loot screen, the encounter is gone and the loot is lost");
                return -1;
            }

            var pending = encounter.RosterToReceiveLootItems;
            int units = pending.Sum(element => element.Amount);
            if (units <= 0)
                return 0;

            // The siege encounter is reused for the next assault, so the screen gets a copy and the pending roster is
            // cleared after the screen opens. A failed open leaves the items pending, not lost.
            var items = new ItemRoster();
            items.Add(pending);
            InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster> { { PartyBase.MainParty, items } });
            pending.Clear();
            return units;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SiegeRetreatLoot] could not open the loot screen, the items stay pending on the encounter: {ex}");
            return -1;
        }
    }
}
