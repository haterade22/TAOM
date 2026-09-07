using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace TAOM.Features.TroopWeight.Hooks;

// Clan-screen party row: restates "X/limit" and its "Party Size:" subtitle in the weighted frame.
// Note this type is also patched by Patch23_BannerColorPersistence (GetCharacterCode) and its rows are
// appended to by the Refuge clan screen — different members, no interaction, but both inherit this text.
[HarmonyPatch(typeof(ClanPartyItemVM), nameof(ClanPartyItemVM.UpdateProperties))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class ClanPartyItemVM_UpdateProperties_Patch
{
    private static IOnClanPartyItemUpdateProperties? _hook;

    public static void Initialize(IOnClanPartyItemUpdateProperties hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(ClanPartyItemVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnClanPartyItemUpdateProperties(__instance);
    }
}
