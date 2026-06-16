using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace TAOM.Features.TroopWeight.Hooks;

// Clan-management screen party list: vanilla ClanPartyItemVM.UpdateProperties builds the "X/limit"
// PartySizeText (and "Party Size: X/limit" subtitle) from the RAW MemberRoster.TotalManCount, so a
// heavy party reads unweighted there even though the party-size budget it's measured against is
// weighted everywhere else. Postfix rewrites both with the weighted member total.
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
