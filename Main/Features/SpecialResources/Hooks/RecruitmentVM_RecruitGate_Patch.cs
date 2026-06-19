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
        if (_hook == null) return;
        if (!__instance.IsDoneEnabled) return; // already blocked (gold / over-limit) — nothing to add

        var cart = __instance.TroopsInCart;
        if (cart == null || cart.Count == 0) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        // Group the cart (one entry per recruited unit) into troopId → count.
        Dictionary<string, int> counts = null;
        foreach (var troop in cart)
        {
            var id = troop?.Character?.StringId;
            if (id == null) continue;
            counts ??= new Dictionary<string, int>();
            counts.TryGetValue(id, out var n);
            counts[id] = n + 1;
        }
        if (counts == null) return;

        var entries = new List<RecruitCartEntry>(counts.Count);
        foreach (var kv in counts)
            entries.Add(new RecruitCartEntry(kv.Key, kv.Value));

        var heroId = hero.StringId;
        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;

        var result = _hook.EvaluateCart(heroId, kingdomId, cultureId, entries);
        if (!result.Blocked) return;

        __instance.IsDoneEnabled = false;
        if (__instance.DoneHint != null)
        {
            var hint = new TextObject("{=taom_recruit_needs_resource}Requires {AMOUNT} {RESOURCE}");
            hint.SetTextVariable("AMOUNT", result.Required);
            hint.SetTextVariable("RESOURCE", result.ResourceDisplayName);
            __instance.DoneHint.HintText = hint;
        }

        _logger?.LogDebug($"[SpecRes] RecruitGate: blocked Done (need {result.Required} {result.ResourceDisplayName})");
    }
}
