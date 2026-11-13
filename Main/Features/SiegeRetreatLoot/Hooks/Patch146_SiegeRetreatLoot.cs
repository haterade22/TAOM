using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Core.Logging;

namespace TAOM.Features.SiegeRetreatLoot.Hooks;

/// <summary>
/// Patch146 (#1121): when the attackers retreat from a siege assault, vanilla's continue handler commits gold, renown,
/// influence and morale, finalizes the battle and opens the siege menu, with no loot from the attackers who fell.
/// The prefix runs vanilla's casualty-loot step first (<see cref="SiegeRetreatLootService"/> holds the rule) and vanilla
/// then runs unchanged; the postfix opens the loot screen after vanilla's menu switch. The two share Harmony's
/// <c>__state</c>. Both catch every exception and leave vanilla's behaviour.
/// </summary>
[HarmonyPatch(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle))]
[HarmonyPatchCategory(Category)]
public static class Patch146_SiegeRetreatLoot
{
    internal const string Category = "Patch146_SiegeRetreatLoot";

    private static SiegeRetreatLootService? _service;
    private static IModLogger? _logger;

    internal static void Initialize(SiegeRetreatLootService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static void Prefix(CampaignBattleResult campaignBattleResult, out bool __state)
    {
        __state = false;
        if (_service == null)
            return;

        try
        {
            __state = _service.CollectLoot(campaignBattleResult != null && campaignBattleResult.EnemyRetreated);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SiegeRetreatLoot] the loot step threw, vanilla's retreat handling runs without it: {ex}");
        }
    }

    [HarmonyPostfix]
    public static void Postfix(bool __state)
    {
        if (!__state || _service == null)
            return;

        try
        {
            _service.OpenLootScreen();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SiegeRetreatLoot] could not open the loot screen: {ex}");
        }
    }
}
