using HarmonyLib;
using TaleWorlds.CampaignSystem.Election;

namespace TAOM.Features.Diplomacy.Hooks;

// A call-to-war ballot for a war the rule refuses can never end in war: the StartCallToWarAgreement
// prefix stops it, and for a war the phase forbids the DeclareWarAction prefix stops it too. Vanilla cancels a decision whose IsAllowed is
// false (KingdomElection.StartElection and the hourly sweep), so the player never sees the offer.
// The rule is ShouldPreventCallToWar: the war veto, or a callToWarRefusals row (#1283).
// The hook defers to the host under co-op. Initialize is wired in DiplomacyIoC.InitializeHooks.
[HarmonyPatch(typeof(ProposeCallToWarAgreementDecision), nameof(ProposeCallToWarAgreementDecision.IsAllowed))]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class ProposeCallToWarAgreementDecision_IsAllowed_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    // A prefix that sets the result and skips the original, not a postfix: IsAllowed has no side
    // effects, and a bool prefix is what CoopVetoClassificationTests scans for.
    [HarmonyPrefix]
    public static bool Prefix(ProposeCallToWarAgreementDecision __instance, ref bool __result)
    {
        if (_hook == null) return true;

        var called = __instance.CalledKingdom;
        var target = __instance.KingdomToCallToWarAgainst;
        if (called == null || target == null) return true;

        if (!_hook.ShouldPreventCallToWar(called.StringId, target.StringId)) return true;

        __result = false;
        return false;
    }
}
