using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Patch42 (T4) — Transpiler on private static
/// <c>AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays</c>. Neutralises the
/// <c>!settlement.IsCastle</c> term in the non-owned-settlement targeting gate (line ~696) so AI also
/// considers allied/neutral castles as recruitment-visit candidates (own-faction castles are already
/// iterated unconditionally). War-state filtering in <c>IsSettlementSuitableForVisitingCondition</c>
/// still applies. Applied together with the AiHourlyTick transpiler for full town-parity travel.
/// Disambiguated by the <c>IsSettlementSuitableForVisitingCondition</c> call after the IsCastle check.
/// </summary>
[HarmonyPatchCategory("Patch42_CastleRecruitment")]
public static class Patch42_FillSettlements_Transpiler
{
    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(AiVisitSettlementBehavior), "FillSettlementsToVisitWithDistancesAsDays");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        CastleAiTranspiler.SwapIsCastleGate(instructions, "IsSettlementSuitableForVisitingCondition", "FillSettlements", _logger);
}
