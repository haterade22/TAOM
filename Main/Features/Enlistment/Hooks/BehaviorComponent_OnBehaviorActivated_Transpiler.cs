using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Fixes the order banner an enlisted soldier reads mid-battle: <c>Men! TaleWorlds.MountAndBlade.BehaviorStop!</c>
/// (field report, screenshot 2026-08-11).
///
/// A VANILLA BUG, verified verbatim on installed 1.4.8 at <c>BehaviorComponent.OnBehaviorActivated</c>:
/// <code>
/// TextObject text = new TextObject(ToString().Replace("MBModule.Behavior", ""));
/// MBTextManager.SetTextVariable("BEHAVIOUR_NAME_BEGIN", text);
/// text = GameTexts.FindText("str_formation_ai_soldier_instruction_text");
/// </code>
/// <c>BehaviorComponent</c> never overrides <c>ToString()</c>, so this is <c>object.ToString()</c> —
/// the fully-qualified type name — and the <c>.Replace</c> is dead code, because no shipped behavior
/// type carries an <c>MBModule.Behavior</c> prefix. The template
/// <c>str_formation_ai_soldier_instruction_text</c> is <c>"Men! {BEHAVIOUR_NAME_BEGIN}!"</c>, which
/// is a character-exact match for what the player photographed.
///
/// The engine already ships both the right call and the right data three methods away:
/// <c>GetBehaviorString()</c> resolves <c>GetType().Name</c> against
/// <c>str_formation_ai_sergeant_instruction_behavior_text</c>, which has 41 authored variations
/// (<c>BehaviorStop</c> → "Wait", <c>BehaviorCharge</c> → "Charge!", …). So the fix is not to invent
/// text — it is to call the accessor vanilla already wrote and forgot to use here.
///
/// WHY A TRANSPILER. The defect is one call inside a method that also runs
/// <c>InformSergeantPlayer()</c>, stamps the private <c>_lastPlayerInformTime</c>, and dispatches
/// <c>OnBehaviorActivatedAux()</c>. A prefix would have to suppress the original and re-implement
/// all three through reflection to fix one string; swapping the single <c>ToString</c> call leaves
/// every other instruction vanilla. The surviving <c>.Replace("MBModule.Behavior", "")</c> then runs
/// against text like "Wait" and is a no-op, exactly as it already was.
///
/// THIS IS NOT ENLISTMENT-ONLY, and that is deliberate rather than scope creep. The broken branch is
/// gated on <c>!IsPlayerGeneral &amp;&amp; !IsPlayerSergeant &amp;&amp; IsPlayerTroopInFormation</c> —
/// a player taking orders inside someone else's formation. In vanilla that state is nearly
/// unreachable, which is why the bug survived; enlistment is what makes it the normal case. The
/// narrowest correct fix and the global one are the same edit.
///
/// SAS does NOT fix this. It leaves the broken banner in place and adds its own on top, so a
/// ServeAsSoldier player below sergeant rank reads both. We suppress nothing and correct the source.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class BehaviorComponent_OnBehaviorActivated_Transpiler
{
    // internal method — resolved by name rather than a nameof() on an inaccessible member.
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(BehaviorComponent), "OnBehaviorActivated");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var describe = AccessTools.Method(
            typeof(BehaviorComponent_OnBehaviorActivated_Transpiler), nameof(DescribeBehavior));

        var replaced = 0;
        for (var i = 0; i < list.Count; i++)
        {
            // Match ToString() by shape, not by a resolved MethodInfo identity: the compiler emits
            // this as object::ToString() today, but a future engine build that gives
            // BehaviorComponent its own override would emit a different token for the same call.
            if ((list[i].opcode == OpCodes.Callvirt || list[i].opcode == OpCodes.Call) &&
                list[i].operand is MethodInfo mi &&
                mi.Name == "ToString" &&
                mi.GetParameters().Length == 0 &&
                mi.ReturnType == typeof(string))
            {
                // ldarg.0 is already on the stack from the original call site, and DescribeBehavior
                // takes the same receiver and returns the same type — so the stack is unchanged.
                list[i] = new CodeInstruction(OpCodes.Call, describe);
                replaced++;
            }
        }

        // SELF-BAIL, per the transpiler convention: if the pattern is gone after an engine bump,
        // hand back the untouched instructions. The banner reverts to vanilla's cosmetic bug rather
        // than the patch throwing inside Harmony's IL pass and taking the mission with it.
        return replaced > 0 ? list : instructions;
    }

    /// <summary>
    /// The name vanilla meant to print. Falls back to the raw type name rather than throwing —
    /// this runs mid-battle on a formation-order callback, where an exception costs the mission and
    /// a wrong-looking banner costs nothing.
    /// </summary>
    public static string DescribeBehavior(BehaviorComponent behavior)
    {
        try
        {
            var text = behavior?.GetBehaviorString();
            var value = text?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        catch (Exception)
        {
            // fall through to the type name
        }

        return behavior?.GetType().Name ?? string.Empty;
    }
}
