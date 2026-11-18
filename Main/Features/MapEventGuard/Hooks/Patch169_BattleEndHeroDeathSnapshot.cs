using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch169: transpiler on <c>MapEventSide.HandleMapEventEndForPartyInternal(PartyBase)</c> (internal, v1.5.7). When two
/// or more heroes of one party are marked <c>DiedInBattle</c> in one battle, the engine's lazy hero filter walks the
/// roster's cached troop list; when a later victim is the party leader, its heir scan rebuilds that list shorter, and the game throws
/// ArgumentOutOfRangeException (a player crash report; Lord Retinue makes such parties common). The one
/// <c>WhereQ</c> call is swapped for <see cref="BattleEndHeroDeathTranspiler.SnapshotWhere{T}"/>, which walks a
/// private copy. Same heroes, same order, same lazy filter. Details: docs/features/map-event-guard.md.
/// </summary>
[HarmonyPatchCategory(BattleEndHeroDeathTranspiler.Category)]
public static class Patch169_BattleEndHeroDeathSnapshot
{
    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    public static MethodBase TargetMethod() => AccessTools.Method(typeof(MapEventSide), "HandleMapEventEndForPartyInternal");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        BattleEndHeroDeathTranspiler.Rewrite(instructions, _logger);
}
