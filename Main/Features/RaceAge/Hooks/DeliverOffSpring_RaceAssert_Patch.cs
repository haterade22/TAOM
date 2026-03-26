using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.RaceAge.Hooks;

[HarmonyPatch(typeof(HeroCreator), "DeliverOffSpring")]
[HarmonyPatchCategory("Patch13_RaceAge")]
public static class DeliverOffSpring_RaceAssert_Patch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var callIndex = -1;

        // Find the SilentAssert call by matching method name on the operand,
        // since CallerXxx default parameter attributes can cause MethodInfo.Equals mismatch
        for (int i = 0; i < newInstructions.Count; i++)
        {
            if (newInstructions[i].opcode == OpCodes.Call &&
                newInstructions[i].operand is MethodInfo mi &&
                mi.Name == "SilentAssert" &&
                mi.DeclaringType?.Name == "Debug")
            {
                callIndex = i;
                break;
            }
        }

        if (callIndex < 0)
            throw new ArgumentException(
                "Cannot find Debug.SilentAssert call in DeliverOffSpring IL. Patch: DeliverOffSpring_RaceAssert_Patch");

        // Walk backwards from the call to find the start of the argument sequence.
        // The IL pattern is:
        //   ldarg.0 (mother)
        //   callvirt get_CharacterObject
        //   callvirt get_Race
        //   ldarg.1 (father)
        //   callvirt get_CharacterObject
        //   callvirt get_Race
        //   ceq
        //   ldstr "" (message)
        //   ldc.i4.0 (getDump)
        //   ldstr "..." (callerFile)
        //   ldstr "..." (callerMethod)
        //   ldc.i4 275 (callerLine)
        //   call Debug.SilentAssert
        //
        // Find ldarg.0 that starts the race comparison by scanning backwards
        var startIndex = -1;

        for (int i = callIndex - 1; i >= 0; i--)
        {
            if (newInstructions[i].opcode == OpCodes.Ldarg_0)
            {
                // Verify this ldarg.0 is followed (within a few instructions) by
                // a call to a property getter named "get_Race"
                for (int j = i + 1; j < callIndex && j <= i + 4; j++)
                {
                    if (newInstructions[j].operand is MethodInfo propGetter &&
                        propGetter.Name == "get_Race")
                    {
                        startIndex = i;
                        break;
                    }
                }

                if (startIndex >= 0)
                    break;
            }
        }

        if (startIndex < 0)
            throw new ArgumentException(
                "Cannot find race comparison start in DeliverOffSpring IL. Patch: DeliverOffSpring_RaceAssert_Patch");

        // NOP out the entire SilentAssert sequence (args + call)
        for (int i = startIndex; i <= callIndex; i++)
        {
            newInstructions[i].opcode = OpCodes.Nop;
            newInstructions[i].operand = null;
        }

        return newInstructions.AsEnumerable();
    }
}
