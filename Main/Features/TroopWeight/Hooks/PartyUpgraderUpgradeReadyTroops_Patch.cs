using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

// Shed-on-upgrade: vanilla PartyUpgraderCampaignBehavior.UpgradeReadyTroops auto-upgrades an AI party's
// ready troops with NO party-size check (it preserves headcount, which in vanilla preserves "weight"
// since every troop is weight 1). TAOM breaks that invariant — a party fills its cap with weight-1
// recruits, then auto-upgrades them into weight-2/3 elites and balloons past its weighted budget.
// This postfix runs once per party AFTER all its upgrades and sheds the cheapest bodies back to budget.
// The method is public; vanilla itself skips MainParty + inactive parties, and the hook re-guards both.
[HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), nameof(PartyUpgraderCampaignBehavior.UpgradeReadyTroops))]
[HarmonyPatchCategory("Patch17_TroopWeight")]
public static class PartyUpgraderUpgradeReadyTroops_Patch
{
    private static IOnPartyUpgraderUpgradeReadyTroops? _hook;

    public static void Initialize(IOnPartyUpgraderUpgradeReadyTroops hook) => _hook = hook;

    [HarmonyPostfix]
    public static void Postfix(PartyBase party)
    {
        if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
        _hook?.OnUpgradeReadyTroops(party);
    }
}
