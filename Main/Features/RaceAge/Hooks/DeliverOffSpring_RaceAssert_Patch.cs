using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.RaceAge.Hooks;

// Noise-reduction (NOT a crash fix). Vanilla HeroCreator.DeliverOffSpring carries a
// Debug.SilentAssert(mother.Race == father.Race). In TAOM, mixed-race couples are normal, so
// this assert fires on every cross-race birth. It is harmless for players — ButterLib's
// DebugManagerWrapper.SilentAssert only writes a Debug-level log line and delegates, and
// MBDebug.SilentAssert only calls Debugger.Break() when a debugger is attached. This transpiler
// NOPs the assert call so it (a) stops breaking a developer's attached debugger on every
// mixed-race birth and (b) drops the recurring "Silent Assert Failed!" debug-log spam.
// Behavior is otherwise unchanged — the birth proceeds identically with or without this patch.
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
        {
            // Anchor gone (already NOPped by a prior application of this transpiler, or the engine IL
            // changed). This patch is pure noise-reduction, so degrade to a no-op instead of throwing
            // out of PatchCategory and crashing the mod (mirrors RefreshCharacterEntityAuxPatch).
            LogTranspilerDegradation("Debug.SilentAssert call not found in DeliverOffSpring IL.");
            return newInstructions.AsEnumerable();
        }

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
        {
            LogTranspilerDegradation("Race-comparison start (ldarg.0 ... get_Race) not found in DeliverOffSpring IL.");
            return newInstructions.AsEnumerable();
        }

        // NOP out the entire SilentAssert sequence (args + call)
        for (int i = startIndex; i <= callIndex; i++)
        {
            newInstructions[i].opcode = OpCodes.Nop;
            newInstructions[i].operand = null;
        }

        return newInstructions.AsEnumerable();
    }

    private static void LogTranspilerDegradation(string detail)
    {
        try
        {
            IoC.Resolve<IModLogger>()?.LogWarning(
                $"[RaceAge] DeliverOffSpring_RaceAssert_Patch transpiler degrading to no-op — {detail} " +
                $"The harmless mixed-race SilentAssert noise-reduction will not apply this session (no gameplay effect).");
        }
        catch { /* logger resolution failure must not surface to the transpiler caller */ }
    }
}
