using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch169, #1475: transpiler on internal <c>MapEventSide.HandleMapEventEndForPartyInternal</c> (v1.5.7
/// MapEventSide.cs:429-453). Vanilla kills each <c>DiedInBattle</c> hero (:435-438) while a lazy <c>WhereQ</c> is
/// still indexing the roster's cached list; a kill that reaches <c>GetTroopRoster()</c> again (MakeDead's own
/// leader search for a second dying hero, or an <c>OnPartyDisbandStarted</c> listener) refills that list shorter
/// and the loop throws <c>ArgumentOutOfRangeException</c>. CrashReport's Continue then leaves the map event half
/// finalized, which a save keeps. A <see cref="BattleDeathRosterTranspiler.Materialize{T}"/> call goes in after the
/// one <c>WhereQ</c>; the <c>WhereQ</c> stays for Coop's transpiler on the same method.
/// Callers, thread, PatchShield and co-op notes: docs/reference/harmony-patch-registry.md
/// "Patch169_BattleDeathRosterSnapshot".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(EncounterGuardModule.BattleDeathSnapshotCategory)]
public static class Patch169_BattleDeathRosterSnapshot
{
    private static IModLogger? _logger;

    internal static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.Method(typeof(MapEventSide), "HandleMapEventEndForPartyInternal");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => BattleDeathRosterTranspiler.InsertSnapshot(instructions, _logger);
}
