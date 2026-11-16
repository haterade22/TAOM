using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.HostilePeace.Hooks;

/// <summary>
/// Patch156, block A: postfix on the private <c>CaravansCampaignBehavior.CanTradeWith(IFaction, IFaction)</c> (v1.5.7, read on
/// the installed DLL 2026-11-15). Vanilla refuses only a faction at war; the caravan picks its destinations through this method
/// (<c>FindNextDestinationForCaravan</c>, and the destination check in <c>HourlyTickParty</c>, which also drops a route
/// mid-way), so a <c>false</c> here keeps a caravan out of a Hostile realm's towns while the two are at peace. A caravan already
/// in or heading into a Hostile town when the block turns on trades there once: <c>OnSettlementEntered</c> sells and the hourly
/// tick buys without asking <c>CanTradeWith</c>.
///
/// <para>Coexists with CaravanTrade's own postfix on the same method (<c>Patch59_CaravanTrade</c>): that one only turns a
/// war-caused <c>false</c> into <c>true</c>, this one only turns a peacetime <c>true</c> into <c>false</c>, so the order of
/// the two does not matter. A fault keeps vanilla's answer. Notes: docs/reference/harmony-patch-registry.md
/// "Patch156_HostilePeace".</para>
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), "CanTradeWith")]
[HarmonyPatchCategory(HostilePeaceModule.PatchCategory)]
public static class Patch156_CaravanTrade_HostilePeace
{
    private static HostilePeaceService? _service;
    private static IModLogger? _logger;
    private static bool _faultLogged;

    public static void Initialize(HostilePeaceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(ref bool __result, IFaction caravanFaction, IFaction targetFaction)
    {
        if (!__result)
            return;
        var service = _service;
        if (service == null || caravanFaction == null || targetFaction == null)
            return;

        try
        {
            if (service.BlocksCaravanTrade(caravanFaction.StringId, targetFaction.StringId, caravanFaction.IsAtWarWith(targetFaction)))
                __result = false;
        }
        catch (Exception ex)
        {
            if (_faultLogged)
                return;
            _faultLogged = true;
            _logger?.LogError($"[HostilePeace] Patch156 caravan check failed, keeping vanilla's answer (logged once): {ex}");
        }
    }
}
