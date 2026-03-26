using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;

namespace TAOM.Features.TroopWeight.Hooks;

[HarmonyPatch(typeof(PartyVM), "PopulatePartyListLabel")]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyVM_PopulatePartyListLabel_Patch
{
    private static IOnPartyVMPopulatePartyListLabel? _hook;

    public static void Initialize(IOnPartyVMPopulatePartyListLabel hook) => _hook = hook;

    [HarmonyPrefix]
    public static bool Prefix(
        ref string __result,
        MBBindingList<PartyCharacterVM> partyList,
        int limit)
    {
        if (!TaomSettings.Instance.EnableTroopWeight) return true;
        if (_hook == null) return true;

        return _hook.OnPartyVMPopulatePartyListLabel(ref __result, partyList, limit);
    }
}
