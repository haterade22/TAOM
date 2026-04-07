using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TAOM.Features.SpecialResources.Hooks;

[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.AddCommand))]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyScreenLogic_AddCommand_Patch
{
    private static IOnPartyUpgradeResourceCheck _hook;
    private static FieldInfo _totalNumberField;
    private static SpecialResourcesBehavior _behavior;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook) => _hook = hook;

    public static void SetBehavior(SpecialResourcesBehavior behavior) => _behavior = behavior;

    public static bool Prefix(PartyScreenLogic __instance, ref PartyScreenLogic.PartyCommand command)
    {
        if (_hook == null) return true;
        if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return true;

        _behavior?.AttachToPartyScreen(__instance);

        var heroId = Hero.MainHero?.StringId;
        if (heroId == null) return true;

        var targetId = command.Character?.UpgradeTargets?[command.UpgradeTarget]?.StringId;
        if (targetId == null) return true;

        var costPerUnit = _hook.GetUpgradeCost(targetId);
        if (costPerUnit <= 0) return true;

        var clamped = _hook.ClampUpgradeCount(heroId, targetId, command.TotalNumber);
        if (clamped <= 0) return false;

        if (clamped < command.TotalNumber)
        {
            SetTotalNumber(ref command, clamped);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Only enough resources for {clamped} upgrade(s).", Colors.Yellow));
        }

        return true;
    }

    private static void SetTotalNumber(ref PartyScreenLogic.PartyCommand command, int value)
    {
        _totalNumberField ??= typeof(PartyScreenLogic.PartyCommand)
            .GetField("<TotalNumber>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(PartyScreenLogic.PartyCommand)
            .GetField("TotalNumber", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        _totalNumberField?.SetValue(command, value);
    }
}
