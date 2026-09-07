using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace TAOM.Features.TroopWeight.Hooks;

// Recruitment-screen capacity readout, restated in the weighted frame (cart included).
//
// COEXISTENCE: Patch51_RecruitmentResourceGate (SpecialResources) already postfixes this same private
// method. It early-outs on !__instance.IsDoneEnabled and only touches the done-button gate; this postfix
// only touches the capacity properties. Neither reads what the other writes, so Harmony's ordering between
// the two categories is not load-bearing — keep it that way.
[HarmonyPatch(typeof(RecruitmentVM), "RefreshPartyProperties")]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class RecruitmentVM_RefreshPartyProperties_Patch
{
    private static IOnRecruitmentVMRefreshPartyProperties? _hook;

    public static void Initialize(IOnRecruitmentVMRefreshPartyProperties hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(RecruitmentVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnRecruitmentRefreshPartyProperties(__instance);
    }
}
