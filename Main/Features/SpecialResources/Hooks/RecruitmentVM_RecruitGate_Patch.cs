using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.Hooks;

// Gate the recruit-volunteers Done button on the player's special-resource balance. Vanilla
// RefreshPartyProperties sets IsDoneEnabled from gold; this postfix ANDs in a resource check so an
// elephant/spider the player can't afford (war_drums / war_spoils) blocks the recruit. Only ever
// forces the flag FALSE — the gold gate is preserved. The actual deduction happens on
// OnUnitRecruitedEvent (SpecialResourcesBehavior); this is the block half only.
//
// A greyed button is not the only way to commit the cart: the Confirm hotkey reaches
// RecruitmentVM.ExecuteDone without reading IsDoneEnabled, so RecruitmentVM_ExecuteDone_Patch (same
// category) re-runs EvaluateCart at that boundary. Both go through EvaluateCart below.
[HarmonyPatch(typeof(RecruitmentVM), "RefreshPartyProperties")]
[HarmonyPatchCategory("Patch51_RecruitmentResourceGate")]
public static class RecruitmentVM_RecruitGate_Patch
{
    private static IOnRecruitmentResourceGate _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnRecruitmentResourceGate hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(RecruitmentVM __instance)
    {
        if (!__instance.IsDoneEnabled) return; // already blocked (gold / over-limit) — nothing to add

        var result = EvaluateCart(__instance);
        if (result == null || !result.Blocked) return;

        __instance.IsDoneEnabled = false;
        if (__instance.DoneHint != null)
            __instance.DoneHint.HintText = RequiresText(result);

        _logger?.LogDebug($"[SpecRes] RecruitGate: blocked Done (need {result.Required} {result.ResourceDisplayName})");
    }

    /// <summary>The cart's verdict in the player's resolved resource, or null when there is nothing to check
    /// (no hook, no hero, empty cart).</summary>
    internal static RecruitGateResult EvaluateCart(RecruitmentVM vm)
    {
        if (_hook == null) return null;

        var cart = vm.TroopsInCart;
        if (cart == null || cart.Count == 0) return null;

        var hero = Hero.MainHero;
        if (hero == null) return null;

        var ids = new List<string>(cart.Count);
        foreach (var troop in cart)
            ids.Add(troop?.Character?.StringId);
        var entries = RecruitCartGrouping.Group(ids);
        if (entries.Count == 0) return null;

        return _hook.EvaluateCart(hero.StringId, hero.Clan?.Kingdom?.StringId, hero.Culture?.StringId, entries);
    }

    internal static TextObject RequiresText(RecruitGateResult result)
    {
        var text = new TextObject("{=taom_recruit_needs_resource}Requires {AMOUNT} {RESOURCE}");
        text.SetTextVariable("AMOUNT", result.Required);
        text.SetTextVariable("RESOURCE", result.ResourceDisplayName);
        return text;
    }

    internal static void LogBlockedCommit(RecruitGateResult result)
        => _logger?.LogInfo($"[SpecRes] RecruitGate: blocked ExecuteDone (need {result.Required} {result.ResourceDisplayName})");
}
