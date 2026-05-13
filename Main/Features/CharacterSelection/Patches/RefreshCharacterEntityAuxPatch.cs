using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterSelection.Patches;

[HarmonyPatch(typeof(BodyGeneratorView), "RefreshCharacterEntityAux")]
[HarmonyPatchCategory("Late_Transpiler")]
public class RefreshCharacterEntityAuxPatch
{
    public static MBActionSet GetActionSet(BodyGeneratorView bodyGeneratorView)
    {
        var race = bodyGeneratorView.BodyGen.Race;
        var monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(race);
        if (monster == null)
            monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(0); // fallback to human
        return MBGlobals.GetActionSetWithSuffix(monster, bodyGeneratorView.BodyGen.IsFemale, "_facegen");
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
    {
        // Phase 9b #160 — degrade gracefully on IL pattern mismatch. Prior to this fix, any of the
        // three lookups (ctor / ActionSet method / Newobj match) throwing ArgumentException at
        // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
        // bricking startup even though the rest of TAOM (and most of CharacterSelection) is
        // unaffected. Soft-fail: log once with the specific gap, return original instructions
        // unchanged, let the game boot. The CharacterSelection face-generator action-set inject
        // doesn't apply, but no other feature breaks.
        var ctor = AccessTools.Constructor(typeof(AgentVisualsData), Type.EmptyTypes);
        if (ctor == null)
        {
            LogTranspilerDegradation("AgentVisualsData parameterless constructor not found via reflection.");
            return instructions;
        }

        var actionSetMethod = typeof(AgentVisualsData).GetMethod(nameof(AgentVisualsData.ActionSet));
        if (actionSetMethod == null)
        {
            LogTranspilerDegradation("AgentVisualsData.ActionSet method not found via reflection.");
            return instructions;
        }

        var newInstructions = new List<CodeInstruction>(instructions);
        var insertionIndex = -1;

        for (int i = 0; i < newInstructions.Count - 1; i++)
        {
            if (newInstructions[i].opcode == OpCodes.Newobj && ctor.Equals(newInstructions[i].operand))
            {
                insertionIndex = i + 1;
                break;
            }
        }

        if (insertionIndex < 0)
        {
            LogTranspilerDegradation("AgentVisualsData Newobj IL pattern not found in BodyGeneratorView.RefreshCharacterEntityAux.");
            return instructions;
        }

        newInstructions.InsertRange(insertionIndex, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RefreshCharacterEntityAuxPatch), nameof(GetActionSet))),
            new CodeInstruction(OpCodes.Callvirt, actionSetMethod)
        });

        return newInstructions.AsEnumerable();
    }

    private static void LogTranspilerDegradation(string detail)
    {
        try
        {
            IoC.Resolve<IModLogger>()?.LogError(
                $"[CharacterSelection] RefreshCharacterEntityAuxPatch transpiler degrading to no-op — {detail} " +
                $"BodyGenerator face-generator action-set injection will not apply this session; " +
                $"verify v1.3.15 IL via ilspycmd on TaleWorlds.MountAndBlade.GauntletUI.dll if behavior is unexpected.");
        }
        catch { /* logger resolution failure must not surface to the transpiler caller */ }
    }
}
