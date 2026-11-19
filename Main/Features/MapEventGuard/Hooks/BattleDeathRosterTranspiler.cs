using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.LinQuick;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// IL surgery for Patch169 (#1475). The engine's end-of-battle loop filters the party roster with a lazy
/// <c>LinQuick.WhereQ&lt;TroopRosterElement&gt;(List, Func)</c> and kills each match inside the loop. That
/// <c>WhereQ</c> caches the count and indexes the list, and the list is the roster's cache, which any
/// <c>GetTroopRoster()</c> call after a kill refills one element shorter. One call to <see cref="Materialize{T}"/>
/// goes in right after the <c>WhereQ</c>, so the loop walks a filled copy. The <c>WhereQ</c> stays: Coop's own
/// transpiler on this method finds it by name and throws when it is gone. Any <c>WhereQ</c> count other than one
/// returns the original stream with one warning, never a half-patched method.
/// </summary>
public static class BattleDeathRosterTranspiler
{
    /// <summary>Runs the lazy filter to the end before the caller's loop starts. Stack shape: IEnumerable in, IEnumerable out.</summary>
    public static IEnumerable<T> Materialize<T>(IEnumerable<T> source) => new List<T>(source);

    public static IEnumerable<CodeInstruction> InsertSnapshot(IEnumerable<CodeInstruction> instructions, IModLogger? logger)
    {
        var list = instructions.ToList();
        var sites = list.Select((ci, i) => (ci, i)).Where(p => IsRosterWhere(p.ci)).ToList();
        if (sites.Count != 1)
        {
            logger?.LogWarning($"[MapEventGuard] expected exactly one WhereQ<TroopRosterElement> call in MapEventSide.HandleMapEventEndForPartyInternal, found {sites.Count}: Patch169 is not applied (#1475)");
            SelfDisables.Report(EncounterGuardModule.BattleDeathSnapshotCategory, $"found {sites.Count} WhereQ<TroopRosterElement> calls, expected 1");
            return list;
        }

        var materialize = AccessTools.Method(typeof(BattleDeathRosterTranspiler), nameof(Materialize))
            .MakeGenericMethod(typeof(TroopRosterElement));
        list.Insert(sites[0].i + 1, new CodeInstruction(OpCodes.Call, materialize));
        return list;
    }

    private static bool IsRosterWhere(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && mi.Name == nameof(LinQuick.WhereQ)
        && mi.DeclaringType == typeof(LinQuick)
        && mi.IsGenericMethod
        && mi.GetGenericArguments().SequenceEqual(new[] { typeof(TroopRosterElement) })
        && mi.GetParameters().Length == 2
        && mi.GetParameters()[0].ParameterType == typeof(List<TroopRosterElement>);
}
