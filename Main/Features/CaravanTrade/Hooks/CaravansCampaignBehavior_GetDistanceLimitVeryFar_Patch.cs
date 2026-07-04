using System;
using HarmonyLib;
using TAOM.Features.CaravanTrade;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.CaravanTrade.Hooks;

/// <summary>
/// Lever 3: widen the vanilla caravan "very far" distance ceiling so profitable distant towns aren't
/// hard-rejected. This is the single read-point for the ceiling — the Close/Medium/Far band getters
/// all derive from it (× 0.75 / 0.5 / 0.25), and both the <c>distanceCut</c> veto and
/// <c>AdjustVeryFarAddition</c> escalation call it — so scaling its return coherently widens the whole
/// tolerance system.
///
/// Patching the read-each-time GETTER (rather than the write-once <c>CacheVeryFarDistances</c> cache)
/// means the MCM master toggle is honored LIVE: turn the feature off mid-session and the ceiling
/// reverts to exact vanilla on the next read, with no persistent field mutation to undo. (Codex
/// review 2026-07-04 MED — the cache-mutation approach left the ceiling scaled after a mid-session
/// toggle-off, breaking the "master-off = exact vanilla immediately" promise.)
///
/// Note: this getter has no per-caravan context, so the widening is GLOBAL — it cannot be scoped off
/// player caravans (documented in the feature doc + the ApplyToPlayerCaravans MCM hint). The selection
/// re-weight (which actually makes a caravan prefer far towns) IS player-scoped, so a player caravan
/// with player-application off still routes by vanilla's nearest-first selection despite the wider ceiling.
/// </summary>
[HarmonyPatch(typeof(CaravansCampaignBehavior), "GetDistanceLimitVeryFarAsDaysForNavigationType")]
[HarmonyPatchCategory("Patch59_CaravanTrade")]
public static class CaravansCampaignBehavior_GetDistanceLimitVeryFar_Patch
{
    private static ICaravanTradeService _service;

    [HarmonyPostfix]
    public static void Postfix(ref float __result)
    {
        // The cache initializes to -1f; only scale a real, computed ceiling (positive) — never the
        // uninitialized sentinel.
        if (!(__result > 0f)) return;

        try
        {
            _service ??= IoC.Resolve<ICaravanTradeService>();
            __result = _service.ScaleVeryFarDistance(__result);
        }
        catch (Exception)
        {
            // IoC not started — leave the vanilla ceiling untouched.
        }
    }
}
