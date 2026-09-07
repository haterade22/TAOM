using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.TroopWeight.Hooks;

// Appends the weight multiplier to a party-screen row's name, so a header reading "19 / 20" over ten
// visible bodies is self-explanatory. Vanilla RefreshValues reassigns Name from the character first, so the
// postfix cannot double-append across repeated calls.
[HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.RefreshValues))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyCharacterVM_RefreshValues_Patch
{
    private static IOnPartyCharacterVMRefreshValues? _hook;

    public static void Initialize(IOnPartyCharacterVMRefreshValues hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyCharacterVM __instance)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnPartyCharacterRefreshValues(__instance);
    }
}
