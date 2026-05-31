using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Shared IL surgery for the two AI-scoring transpilers. Swaps the recruitment-gate <c>get_IsCastle</c>
/// for a static call to <see cref="CastleAiToggle"/>.
///
/// Targets the FIRST <c>get_IsCastle</c> in the method — in both target methods the recruitment gate is
/// the first (AiHourlyTick: line ~269 recruit gate vs. the later line ~317 reform-score gate;
/// FillSettlements: a single get_IsCastle) — and additionally requires a uniquely-named anchor method
/// to appear shortly after it. This double check means a future engine refactor that reorders or
/// renames the gate makes the patch FAIL-SAFE (log + return the original stream, AI keeps vanilla
/// behaviour) rather than swap the wrong IsCastle. We never search past the first get_IsCastle, so the
/// later reform-score gate can never be hit by accident.
/// </summary>
internal static class CastleAiTranspiler
{
    private const int AnchorWindow = 24;

    internal static List<CodeInstruction> SwapIsCastleGate(
        IEnumerable<CodeInstruction> instructions, string anchorMethodName, string label, IModLogger? logger)
    {
        var list = new List<CodeInstruction>(instructions);
        var getIsCastle = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.IsCastle));
        var replacement = AccessTools.Method(typeof(CastleAiToggle), nameof(CastleAiToggle.IsCastleAndAiDisabled));

        if (getIsCastle == null || replacement == null)
        {
            logger?.LogWarning($"[CastleRecruitment] {label}: reflection lookup failed — AI castle gate not patched (vanilla behaviour preserved)");
            return list;
        }

        int firstIsCastle = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (IsGetIsCastle(list[i], getIsCastle))
            {
                firstIsCastle = i;
                break;
            }
        }

        if (firstIsCastle < 0)
        {
            logger?.LogWarning($"[CastleRecruitment] {label}: no get_IsCastle found — AI castle gate not patched (vanilla behaviour preserved)");
            return list;
        }

        // Confirm the FIRST get_IsCastle is the recruitment gate by requiring the expected anchor
        // method shortly after it. If absent, bail — never fall through to a different get_IsCastle.
        bool anchored = false;
        int end = Math.Min(list.Count, firstIsCastle + 1 + AnchorWindow);
        for (int j = firstIsCastle + 1; j < end; j++)
        {
            if (IsCallNamed(list[j], anchorMethodName))
            {
                anchored = true;
                break;
            }
        }

        if (!anchored)
        {
            logger?.LogWarning($"[CastleRecruitment] {label}: first get_IsCastle not followed by anchor '{anchorMethodName}' — AI castle gate not patched (vanilla behaviour preserved)");
            return list;
        }

        // Same stack shape: callvirt instance get_IsCastle(Settlement)->bool  becomes
        // call static IsCastleAndAiDisabled(Settlement)->bool. Labels remain on the same object.
        list[firstIsCastle].opcode = OpCodes.Call;
        list[firstIsCastle].operand = replacement;
        logger?.LogDebug($"[CastleRecruitment] {label}: IsCastle gate swapped at instruction {firstIsCastle}");
        return list;
    }

    private static bool IsGetIsCastle(CodeInstruction ci, MethodInfo getIsCastle) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && mi.Name == getIsCastle.Name
        && mi.DeclaringType == getIsCastle.DeclaringType;

    private static bool IsCallNamed(CodeInstruction ci, string name) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi
        && mi.Name == name;
}
