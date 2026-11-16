using HarmonyLib;
using TaleWorlds.CampaignSystem.Election;

namespace TAOM.Features.Diplomacy.Hooks;

// The called kingdom's side of the same ballot (see ProposeCallToWarAgreementDecision_IsAllowed_Patch):
// accepting would declare war between the deciding kingdom and the target, so a refused war cancels
// the offer before the player sees it. Initialize is wired in DiplomacyIoC.InitializeHooks.
[HarmonyPatch(typeof(AcceptCallToWarAgreementDecision), nameof(AcceptCallToWarAgreementDecision.IsAllowed))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class AcceptCallToWarAgreementDecision_IsAllowed_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    // A prefix that sets the result and skips the original, not a postfix: IsAllowed has no side
    // effects, and a bool prefix is what CoopVetoClassificationTests scans for.
    [HarmonyPrefix]
    public static bool Prefix(AcceptCallToWarAgreementDecision __instance, ref bool __result)
    {
        if (_hook == null) return true;

        var called = __instance.Kingdom;
        var target = __instance.KingdomToCallToWarAgainst;
        if (called == null || target == null) return true;

        if (!_hook.ShouldPreventCallToWar(called.StringId, target.StringId)) return true;

        __result = false;
        return false;
    }
}
