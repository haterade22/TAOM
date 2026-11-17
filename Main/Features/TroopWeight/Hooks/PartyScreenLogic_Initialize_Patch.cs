using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

// Records, when a party screen opens, whether each side's limit is its owner's own (#1430). Initialize copies each
// limit once (v1.5.7 :491, :494) while the normal screen edits the live roster, so this is the last point where the
// copy and the party's weight-deflated limit can be compared. Postfix: Initialize has set both limits by then.
[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyScreenLogic_Initialize_Patch
{
    private static IOnPartyScreenLogicTransfer? _hook;

    public static void Initialize(IOnPartyScreenLogicTransfer hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyScreenLogic __instance) => _hook?.OnInitialize(__instance);
}
