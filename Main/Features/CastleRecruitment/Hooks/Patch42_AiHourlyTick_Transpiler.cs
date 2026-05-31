using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Patch42 (T3) — Transpiler on private <c>AiVisitSettlementBehavior.AiHourlyTick</c>. Neutralises the
/// <c>!settlement.IsCastle</c> term in the recruitment-desirability gate
/// (<c>if (!settlement.IsCastle &amp;&amp; item &lt; 1f &amp;&amp; mobileParty.GetAvailableWageBudget() &gt; 0)</c>)
/// so AI lord parties SCORE castles for recruitment and therefore travel to them. Gated at runtime by
/// <see cref="CastleAiToggle"/> (the swapped call returns the real IsCastle when AI castle recruitment
/// is off). Disambiguated by the <c>GetAvailableWageBudget</c> call that follows the IsCastle check.
/// </summary>
[HarmonyPatchCategory("Patch42_CastleRecruitment")]
public static class Patch42_AiHourlyTick_Transpiler
{
    private static IModLogger? _logger;

    public static void Initialize(IModLogger logger) => _logger = logger;

    public static MethodBase TargetMethod() => AccessTools.Method(typeof(AiVisitSettlementBehavior), "AiHourlyTick");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        CastleAiTranspiler.SwapIsCastleGate(instructions, "GetAvailableWageBudget", "AiHourlyTick", _logger);
}
