using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.BattleGearLoot.Hooks;

/// <summary>
/// Patch172: replaces the player's body loot for one fallen enemy with the gear that enemy fought in
/// (<see cref="BattleGearLootService"/> holds the rule). Returns true, leaving vanilla untouched, unless the service
/// applies and the winner is the player's party. The AI branch, winner assignment and loot screen stay vanilla.
/// </summary>
[HarmonyPatch(typeof(MapEvent), "LootCasualtyCharacter")]
[HarmonyPatchCategory(Category)]
public static class Patch172_BattleGearLoot
{
    internal const string Category = "Patch172_BattleGearLoot";

    private static BattleGearLootService? _service;
    private static IModLogger? _logger;
    private static int _lastFailedBattle;

    internal static void Initialize(BattleGearLootService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(MapEvent __instance, CharacterObject casualtyCharacter, MapEventParty winnerParty,
        int maxLootedItemsPerBodyForMainParty, ItemRoster mainPartyLootFromCasualties)
    {
        if (_service == null || mainPartyLootFromCasualties == null || maxLootedItemsPerBodyForMainParty <= 0 || !_service.Applies())
            return true;

        CasualtyGearAdapter? adapter = null;
        try
        {
            if (winnerParty?.Party?.LeaderHero != Hero.MainHero || casualtyCharacter == null)
                return true;

            adapter = CasualtyGearAdapter.For(casualtyCharacter, mainPartyLootFromCasualties);
            _service.LootCasualty(adapter, RuntimeHelpers.GetHashCode(__instance).ToString());
            return false;
        }
        catch (Exception ex)
        {
            int battle = RuntimeHelpers.GetHashCode(__instance);
            if (battle != _lastFailedBattle)
            {
                _lastFailedBattle = battle;
                _logger?.LogError($"[BattleGearLoot] the gear loot step threw, {(adapter?.Granted == true ? "the body keeps what was granted" : "vanilla's loot runs")}: {ex}");
            }

            return adapter?.Granted != true;
        }
    }
}
