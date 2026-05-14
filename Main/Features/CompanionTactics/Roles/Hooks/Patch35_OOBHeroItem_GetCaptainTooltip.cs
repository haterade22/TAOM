using System;
using System.Collections.Generic;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;

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
    // Phase 9b #164 — one-shot logging so a future tooltip decorator failure surfaces ONE
    // diagnostic line instead of being swallowed forever. Per-tooltip log spam would render the
    // log unusable, so guard with a process-lifetime flag.
    private static bool _exceptionLogged;

    public static void Postfix(OrderOfBattleHeroItemVM __instance, ref List<TooltipProperty> __result)
    {
        try
        {
            if (__result == null || __result.Count == 0) return;
            _decorator ??= IoC.Resolve<IRoleTooltipDecorator>();
            _decorator?.AppendRoleToCaptainTooltip(__instance, __result);
        }
        catch (Exception ex)
        {
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try { IoC.Resolve<IModLogger>()?.LogError(
                    $"[CompanionTactics] OOBHeroItem GetCaptainTooltip postfix failed: " +
                    $"{ex.GetType().Name}: {ex.Message}. Tooltip role decoration disabled this session " +
                    $"(one-shot log)."); }
                catch { /* logger unavailable — keep silent fallback */ }
            }
        }
    }
}
