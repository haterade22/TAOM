using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.SpecialResources.Hooks;

[HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.InitializeUpgrades))]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyCharacterVM_InitializeUpgrades_Patch
{
    private static IOnPartyUpgradeResourceCheck _hook;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook) => _hook = hook;

    public static void Postfix(PartyCharacterVM __instance)
    {
        if (_hook == null) return;
        if (!__instance.IsUpgradableTroop) return;

        var heroId = Hero.MainHero?.StringId;
        var kingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        if (heroId == null || kingdomId == null) return;

        var resourceName = _hook.GetResourceDisplayName(kingdomId);
        if (resourceName == null) return;

        for (int i = 0; i < __instance.Upgrades.Count; i++)
        {
            var upgrade = __instance.Upgrades[i];
            if (upgrade.AvailableUpgrades <= 0) continue;

            var targetId = __instance.Character.UpgradeTargets[i]?.StringId;
            if (targetId == null) continue;

            var resourceCost = _hook.GetUpgradeCost(targetId);
            if (resourceCost <= 0) continue;

            var currentAmount = _hook.GetCurrentAmount(heroId);
            var maxAffordable = (int)(currentAmount / resourceCost);
            if (maxAffordable < upgrade.AvailableUpgrades)
            {
                var clamped = System.Math.Max(0, maxAffordable);
                var isDisabled = clamped == 0;
                var costHint = $"{resourceName} cost: {resourceCost} per troop (have {currentAmount:F0})";

                upgrade.Refresh(
                    clamped,
                    upgrade.IsAvailable,
                    isDisabled || !upgrade.IsAvailable,
                    true,
                    true,
                    costHint,
                    upgrade.IsMarinerTroop);
            }
        }
    }
}
