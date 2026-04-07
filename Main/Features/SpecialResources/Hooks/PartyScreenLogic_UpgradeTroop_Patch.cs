using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.SpecialResources.Hooks;

[HarmonyPatch(typeof(PartyScreenLogic), "UpgradeTroop")]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyScreenLogic_UpgradeTroop_Patch
{
    private static IOnPartyUpgradeResourceCheck _hook;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook) => _hook = hook;

    public static void Postfix(PartyScreenLogic __instance, TaleWorlds.CampaignSystem.Roster.TroopRosterElement element, int upgradeTargetIndex, int count)
    {
        if (_hook == null) return;
        if (count <= 0) return;

        var heroId = Hero.MainHero?.StringId;
        if (heroId == null) return;

        var targetId = element.Character?.UpgradeTargets?[upgradeTargetIndex]?.StringId;
        if (targetId == null) return;

        var costPerUnit = _hook.GetUpgradeCost(targetId);
        if (costPerUnit <= 0) return;

        _hook.SpendForUpgrade(heroId, targetId, count);
    }
}
