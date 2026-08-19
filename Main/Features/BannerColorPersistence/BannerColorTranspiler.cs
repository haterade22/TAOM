using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.BannerColorPersistence;

/// <summary>
/// IL surgery for the v1.5.0 party-icon colour path.
/// <para>
/// Until v1.4.8 this was a plain postfix: <c>MobilePartyVisual.AddCharacterToPartyIcon</c> took
/// <c>ref uint teamColor1, ref uint teamColor2</c> and TAOM simply overwrote them. v1.5.0 cut that
/// method from eleven parameters to seven, deleting both, and moved the work into the new helper
/// <c>SandBoxViewHelpers.MobilePartyVisualHelper.GetHumanAgentPartyVisual</c>, where the colours are
/// now locals read from <c>party.MapFaction.Color</c> / <c>.Color2</c> and fed to
/// <c>AgentVisualsData.ClothColor1</c> / <c>ClothColor2</c>. There is no parameter left to override,
/// so the seam moved from the signature into the body.
/// </para>
/// <para>
/// <c>MapFaction</c> is the KINGDOM for a clan in one, which is precisely why the override exists:
/// TAOM shows a clan its own heraldic colours rather than its liege's.
/// </para>
/// Each swap appends <c>ldarg.2</c> (the <c>PartyBase party</c> argument, index 2 of the static
/// helper) plus a call to a resolver taking <c>(uint vanillaColor, PartyBase party)</c>. That is
/// stack-neutral: the resolver pops the two it pushed and returns one <c>uint</c>. The vanilla value
/// is passed through so the resolver can return it unchanged whenever the clan-colour rule does not
/// apply, which keeps the null-faction fallback branch working untouched.
/// If either site is absent, the swap is skipped with a warning and vanilla colours survive. Never
/// throw: a category re-apply must not crash.
/// </summary>
internal static class BannerColorTranspiler
{
    internal static List<CodeInstruction> Rewrite(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo? resolveColor1,
        MethodInfo? resolveColor2,
        IModLogger? logger)
    {
        var list = new List<CodeInstruction>(instructions);

        if (resolveColor1 == null || resolveColor2 == null)
        {
            logger?.LogWarning(
                "[BannerColor] resolver lookup failed, party-icon clan colours not patched (vanilla faction colours preserved)");
            return list;
        }

        if (!TryAppendResolver(list, "get_Color", resolveColor1))
            logger?.LogWarning(
                "[BannerColor] primary colour site (callvirt IFaction::get_Color) not found, left vanilla");

        if (!TryAppendResolver(list, "get_Color2", resolveColor2))
            logger?.LogWarning(
                "[BannerColor] secondary colour site (callvirt IFaction::get_Color2) not found, left vanilla");

        return list;
    }

    // Finds the first `callvirt IFaction::<getterName>` and inserts `ldarg.2; call resolver`
    // directly after it. Returns false when the site is absent.
    private static bool TryAppendResolver(List<CodeInstruction> list, string getterName, MethodInfo resolver)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i].opcode != OpCodes.Callvirt && list[i].opcode != OpCodes.Call)
                || list[i].operand is not MethodInfo mi
                || mi.Name != getterName
                || mi.DeclaringType?.Name != "IFaction")
                continue;

            list.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_2));
            list.Insert(i + 2, new CodeInstruction(OpCodes.Call, resolver));
            return true;
        }
        return false;
    }
}
