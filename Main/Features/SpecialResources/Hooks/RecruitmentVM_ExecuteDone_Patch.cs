using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.SpecialResources.Hooks;

// The commit-boundary half of Patch51. v1.5.3's GauntletMenuRecruitVolunteersView.OnFrameTick routes
// the Confirm hotkey straight to RecruitmentVM.ExecuteDone without reading IsDoneEnabled, and OnDone
// then rechecks gold only, so the greyed Done button alone let a player with 40 Castar recruit a
// 45-Castar Ranger and pay 40 (Codex review of #600, M1). This prefix re-runs the same cart verdict
// and skips the commit when the cart is unaffordable. ExecuteDone is the one public entry the button,
// the hotkey and the over-limit inquiry all pass through; the quit path calls ExecuteReset first, so
// its cart is empty and passes.
[HarmonyPatch(typeof(RecruitmentVM), "ExecuteDone")]
[HarmonyPatchCategory("Patch51_RecruitmentResourceGate")]
public static class RecruitmentVM_ExecuteDone_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(RecruitmentVM __instance)
    {
        var result = RecruitmentVM_RecruitGate_Patch.EvaluateCart(__instance);
        if (result == null || !result.Blocked) return true;

        InformationManager.DisplayMessage(new InformationMessage(
            RecruitmentVM_RecruitGate_Patch.RequiresText(result).ToString(), Colors.Red));
        RecruitmentVM_RecruitGate_Patch.LogBlockedCommit(result);
        return false;
    }
}
