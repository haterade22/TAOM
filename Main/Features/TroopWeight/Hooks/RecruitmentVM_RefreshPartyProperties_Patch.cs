using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace TAOM.Features.TroopWeight.Hooks;

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
        _hook?.OnRecruitmentVMRefreshPartyProperties(__instance);
    }
}
