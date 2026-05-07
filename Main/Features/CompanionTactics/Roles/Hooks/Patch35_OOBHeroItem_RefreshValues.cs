using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.Roles.Hooks;

/// <summary>
/// Postfix on <c>OrderOfBattleHeroItemVM.RefreshValues</c>. Runs at LowerThanNormal priority so
/// it executes AFTER Patch23 (banner color persistence) — the order matters because Patch23
/// rewrites tooltip properties and we append the role prefix on top.
/// </summary>
[HarmonyPatch(typeof(OrderOfBattleHeroItemVM), nameof(OrderOfBattleHeroItemVM.RefreshValues))]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
[HarmonyPriority(Priority.LowerThanNormal)]
public static class Patch35_OOBHeroItem_RefreshValues
{
    private static IRoleTooltipDecorator _decorator;

    [HarmonyPostfix]
    public static void Postfix(OrderOfBattleHeroItemVM __instance)
    {
        try
        {
            _decorator ??= IoC.Resolve<IRoleTooltipDecorator>();
            _decorator?.DecorateOOBHeroItem(__instance);
        }
        catch { }
    }
}
