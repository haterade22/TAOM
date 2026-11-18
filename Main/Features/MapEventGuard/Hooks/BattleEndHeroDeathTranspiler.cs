using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.LinQuick;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// IL surgery and helper for Patch169. <c>MapEventSide.HandleMapEventEndForPartyInternal</c> builds a lazy
/// <c>party.MemberRoster.GetTroopRoster().WhereQ(...)</c> over the roster's ONE cached list, then kills the
/// matching heroes in a loop. A kill removes the hero; the next roster read (a leader's heir scan) rebuilds that
/// cached list in place shorter while <c>WhereQ</c> still walks the length it captured: ArgumentOutOfRangeException. The single <c>LinQuick.WhereQ&lt;TroopRosterElement&gt;(List, Func)</c> call is swapped for
/// <see cref="SnapshotWhere{T}"/>, which walks a private copy. Any call count other than one returns the original
/// stream untouched with one warning.
/// </summary>
internal static class BattleEndHeroDeathTranspiler
{
    internal const string Category = "Patch169_BattleEndHeroDeathSnapshot";

    internal static readonly MethodInfo? VanillaCall = FindWhereQ();

    internal static readonly MethodInfo HookCall =
        AccessTools.Method(typeof(BattleEndHeroDeathTranspiler), nameof(SnapshotWhere))
            .MakeGenericMethod(typeof(TroopRosterElement));

    /// <summary>
    /// Same signature as <c>LinQuick.WhereQ&lt;T&gt;(List&lt;T&gt;, Func&lt;T, bool&gt;)</c>. The copy is taken now; the filter
    /// still runs lazily, in order, so each hero is tested just before its own kill exactly as in vanilla.
    /// </summary>
    internal static IEnumerable<T> SnapshotWhere<T>(List<T> source, Func<T, bool> predicate)
    {
        // An iterator: the copy is taken on the first MoveNext, the moment vanilla's WhereQ first reads the list.
        foreach (var item in new List<T>(source))
        {
            if (predicate(item))
                yield return item;
        }
    }

    internal static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, IModLogger? logger)
    {
        var list = instructions.ToList();
        var sites = list.Where(IsVanillaCall).ToList();
        if (sites.Count != 1)
        {
            logger?.LogWarning($"[MapEventGuard] expected exactly one WhereQ<TroopRosterElement> call in MapEventSide.HandleMapEventEndForPartyInternal, found {sites.Count}: the battle-end hero death snapshot is not applied");
            SelfDisables.Report(Category, $"found {sites.Count} WhereQ calls, expected 1");
            return list;
        }

        sites[0].opcode = System.Reflection.Emit.OpCodes.Call;
        sites[0].operand = HookCall;
        return list;
    }

    private static MethodInfo? FindWhereQ()
    {
        foreach (var m in typeof(LinQuick).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "WhereQ" || !m.IsGenericMethodDefinition) continue;
            var p = m.GetParameters();
            if (p.Length == 2 && p[0].ParameterType.IsGenericType
                && p[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>))
                return m.MakeGenericMethod(typeof(TroopRosterElement));
        }
        return null;
    }

    private static bool IsVanillaCall(CodeInstruction ci) =>
        (ci.opcode == System.Reflection.Emit.OpCodes.Call || ci.opcode == System.Reflection.Emit.OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && VanillaCall != null
        && mi == VanillaCall;
}
