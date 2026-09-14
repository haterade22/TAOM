using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyItem;

namespace TAOM.Features.TroopWeight.Hooks;

// Clan-screen party row: restates "X/limit" and its "Party Size:" subtitle in the weighted frame.
// Note this type is also patched by Patch23_BannerColorPersistence (GetCharacterCode) and its rows are
// appended to by the Refuge clan screen — different members, no interaction, but both inherit this text.
//
// v1.5.x: ClanPartyItemVM.UpdateProperties is ABSTRACT. The work moved into the two concrete rows,
// ClanPartyItemWithPartyVM (garrisons, caravans, war parties) and ClanPartyItemWithHeroVM (a
// party-less member row). An abstract method has no body for Harmony to patch, so the target is
// both overrides; the postfix still receives the row as its base type.
[HarmonyPatch]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class ClanPartyItemVM_UpdateProperties_Patch
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ClanPartyItemWithPartyVM), nameof(ClanPartyItemWithPartyVM.UpdateProperties));
        yield return AccessTools.Method(typeof(ClanPartyItemWithHeroVM), nameof(ClanPartyItemWithHeroVM.UpdateProperties));
    }

    private static IOnClanPartyItemUpdateProperties? _hook;

    public static void Initialize(IOnClanPartyItemUpdateProperties hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(ClanPartyItemVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnClanPartyItemUpdateProperties(__instance);
    }
}
