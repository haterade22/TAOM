using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.TroopWeight.Hooks;

// Party-screen troop header. Postfix on the CALLER, not on PartyVM.PopulatePartyListLabel: that builder is
// `private static`, is handed no party, and produces the PRISONER headers from the same code path — so
// patching it would weight prisoner counts too. RefreshPartyInformation has __instance, which reaches both
// the owning parties and the two troop labels. Vanilla runs first and we restate its result, so nothing is
// suppressed. Private method -> patched by string name (v1.4.8-verified).
[HarmonyPatch(typeof(PartyVM), "RefreshPartyInformation")]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyVM_RefreshPartyInformation_Patch
{
    private static IOnPartyVMRefreshPartyInformation? _hook;

    public static void Initialize(IOnPartyVMRefreshPartyInformation hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnRefreshPartyInformation(__instance);
    }
}
