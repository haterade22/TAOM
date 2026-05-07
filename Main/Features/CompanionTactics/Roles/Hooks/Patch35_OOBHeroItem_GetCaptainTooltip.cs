using System.Collections.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.Roles.Hooks;

/// <summary>
/// Manual postfix for the PRIVATE method <c>OrderOfBattleHeroItemVM.GetCaptainTooltip</c>.
/// In v1.3.15 the method is private; <c>[HarmonyPatch]</c> attribute binding cannot resolve
/// it. The dispatching session wires this up manually in <c>SubModule.cs</c> via
/// <c>AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip")</c>.
///
/// Postfix signature MUST match Harmony's expectation:
///   (TargetType __instance, ref ReturnType __result, ...)
/// </summary>
public static class Patch35_OOBHeroItem_GetCaptainTooltip
{
    private static IRoleTooltipDecorator _decorator;

    public static void Postfix(OrderOfBattleHeroItemVM __instance, ref List<TooltipProperty> __result)
    {
        try
        {
            if (__result == null || __result.Count == 0) return;
            _decorator ??= IoC.Resolve<IRoleTooltipDecorator>();
            _decorator?.AppendRoleToCaptainTooltip(__instance, __result);
        }
        catch { }
    }
}
