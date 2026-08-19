using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.PartyIconScale;

/// <summary>
/// IL surgery for <c>Patch53_PartyIconScale</c>. Rewrites the two hardcoded <c>0.3f</c> scale literals in
/// <c>MobilePartyVisual.AddCharacterToPartyIcon</c> into <c>call PartyIconScaleConfig.GetScale()</c> so the
/// campaign-map party-icon figure and its mount honour the MCM "Map Figure Scale" slider.
/// <list type="bullet">
///   <item><b>People:</b> <c>ldc.r4 0.3</c> immediately before <c>callvirt AgentVisualsData::Scale</c>
///   (<c>.Scale(0.3f)</c>).</item>
///   <item><b>Mount:</b> <c>ldc.r4 0.3</c> immediately before <c>mul</c>
///   (<c>.Scale(item.ScaleFactor * 0.3f)</c>).</item>
/// </list>
/// Both swaps are stack-neutral: <c>ldc.r4</c> and the parameterless <c>float GetScale()</c> each push one
/// float and pop none. The <c>ldc.r4</c> is mutated <i>in place</i> so any branch labels/exception blocks on
/// it survive. The other <c>0.3</c> literals in the method feed animation-speed math (<c>… / 0.3f</c> =
/// <c>div</c>) and are not matched. If either site is absent (engine refactor), the swap is skipped with a
/// warning and vanilla <c>0.3</c> is preserved — never throw, so a category re-apply can't crash.
/// </summary>
internal static class PartyIconScaleTranspiler
{
    // v1.5.0 split the three scale sites across TWO methods, so there are now two entry points.
    //
    // AddCharacterToPartyIcon keeps the MOUNT site (ldc.r4 0.3 -> mul) and gained a NEW hardcoded
    // human world-frame scale (ldc.r4 0.3 -> call Mat3.ApplyScaleLocal). In v1.4.8 that frame read
    // its scale back off the AgentVisualsData this transpiler had already rewritten, so one swap
    // covered both; v1.5.0 hardcodes it and it must be swapped explicitly or the rider's world
    // frame stays vanilla-size while its visual shrinks.
    internal static List<CodeInstruction> RewriteIconSites(
        IEnumerable<CodeInstruction> instructions, MethodInfo? getScale, IModLogger? logger)
    {
        var list = new List<CodeInstruction>(instructions);
        if (!GuardGetScale(getScale, logger)) return list;

        if (!TrySwapZeroPointThree(list, getScale!, NextIsMul))
            logger?.LogWarning(
                "[PartyIconScale] mount scale site (ldc.r4 0.3 -> mul) not found, left vanilla");

        if (!TrySwapZeroPointThree(list, getScale!, NextIsApplyScaleLocal))
            logger?.LogWarning(
                "[PartyIconScale] human frame scale site (ldc.r4 0.3 -> call ApplyScaleLocal) not found, left vanilla");

        return list;
    }

    // The PEOPLE site moved out of AddCharacterToPartyIcon entirely in v1.5.0 and now lives in
    // SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper.GetHumanAgentPartyVisual as a bare
    // .Scale(0.3f). Harmony never feeds one method's IL to another method's transpiler, so this
    // needs its own patch target rather than a wider matcher.
    internal static List<CodeInstruction> RewriteHumanVisualSite(
        IEnumerable<CodeInstruction> instructions, MethodInfo? getScale, IModLogger? logger)
    {
        var list = new List<CodeInstruction>(instructions);
        if (!GuardGetScale(getScale, logger)) return list;

        if (!TrySwapZeroPointThree(list, getScale!, NextIsScaleCall))
            logger?.LogWarning(
                "[PartyIconScale] people scale site (ldc.r4 0.3 -> callvirt Scale) not found, left vanilla");

        return list;
    }

    private static bool GuardGetScale(MethodInfo? getScale, IModLogger? logger)
    {
        if (getScale != null) return true;
        logger?.LogWarning(
            "[PartyIconScale] GetScale lookup failed, party-icon scale not patched (vanilla 0.3 preserved)");
        return false;
    }

    // Finds the first `ldc.r4 0.3` whose following instruction matches <paramref name="nextMatches"/> and
    // replaces it in place with `call getScale`. Returns false if no such site exists.
    private static bool TrySwapZeroPointThree(
        List<CodeInstruction> list, MethodInfo getScale, Func<CodeInstruction, bool> nextMatches)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            if (list[i].opcode == OpCodes.Ldc_R4
                && list[i].operand is float f && f == 0.3f
                && nextMatches(list[i + 1]))
            {
                list[i].opcode = OpCodes.Call;
                list[i].operand = getScale;
                return true;
            }
        }
        return false;
    }

    private static bool NextIsScaleCall(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call)
        && ci.operand is MethodInfo mi && mi.Name == "Scale";

    private static bool NextIsMul(CodeInstruction ci) => ci.opcode == OpCodes.Mul;

    private static bool NextIsApplyScaleLocal(CodeInstruction ci) =>
        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
        && ci.operand is MethodInfo mi && mi.Name == "ApplyScaleLocal";
}
