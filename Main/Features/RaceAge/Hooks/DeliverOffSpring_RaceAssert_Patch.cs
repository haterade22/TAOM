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
        var silentAssert = AccessTools.Method(
            typeof(Debug), nameof(Debug.SilentAssert),
            new[] { typeof(bool), typeof(string), typeof(bool), typeof(string), typeof(string), typeof(int) });

        if (silentAssert == null)
            throw new ArgumentException(
                "Cannot find Debug.SilentAssert method. Patch: DeliverOffSpring_RaceAssert_Patch");

        var newInstructions = new List<CodeInstruction>(instructions);
        var callIndex = -1;

        for (int i = 0; i < newInstructions.Count; i++)
        {
            if (newInstructions[i].opcode == OpCodes.Call &&
                silentAssert.Equals(newInstructions[i].operand))
            {
                callIndex = i;
                break;
            }
        }

        if (callIndex < 0)
            throw new ArgumentException(
                "Cannot find Debug.SilentAssert call in DeliverOffSpring IL. Patch: DeliverOffSpring_RaceAssert_Patch");

        // SilentAssert takes 6 parameters. Walk backwards from the call to find
        // the start of the argument loading sequence. The IL pattern is:
        //   load mother, get CharacterObject, get Race
        //   load father, get CharacterObject, get Race
        //   ceq
        //   ldstr "" (message)
        //   ldc.i4.0 (getDump)
        //   ldstr "..." (callerFile)
        //   ldstr "..." (callerMethod)
        //   ldc.i4 275 (callerLine)
        //   call Debug.SilentAssert
        //
        // We NOP from the first instruction that loads arguments for this assert
        // through the call itself. Scan backwards to find where arg loading begins.
        // The race comparison starts with loading the mother argument (ldarg.0 for
        // a static method where mother is the first parameter).

        // Find the start by looking for the Race property getter sequence.
        // Walk backwards from callIndex to find the earliest instruction that feeds
        // into this SilentAssert. We look for the pattern starting with ldarg.0
        // (mother parameter) before any get_Race call.
        var raceGetter = AccessTools.PropertyGetter(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Race));
        var startIndex = -1;

        for (int i = callIndex - 1; i >= 0; i--)
        {
            if (newInstructions[i].opcode == OpCodes.Ldarg_0)
            {
                // Verify this ldarg.0 is followed by a path that reaches get_Race
                for (int j = i + 1; j < callIndex && j <= i + 3; j++)
                {
                    if (raceGetter != null && raceGetter.Equals(newInstructions[j].operand))
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

        // NOP out the entire SilentAssert sequence
        for (int i = startIndex; i <= callIndex; i++)
        {
            newInstructions[i].opcode = OpCodes.Nop;
            newInstructions[i].operand = null;
        }

        return newInstructions.AsEnumerable();
    }
}
