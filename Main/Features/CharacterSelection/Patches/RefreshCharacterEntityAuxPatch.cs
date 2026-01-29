using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CharacterSelection.Patches;

[HarmonyPatch(typeof(BodyGeneratorView), "RefreshCharacterEntityAux")]
public class RefreshCharacterEntityAuxPatch
{
    public static MBActionSet GetActionSet(BodyGeneratorView bodyGeneratorView)
    {
        var baseMonsterFromRace = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(bodyGeneratorView.BodyGen.Race);
        return MBGlobals.GetActionSetWithSuffix(baseMonsterFromRace, bodyGeneratorView.BodyGen.IsFemale, "_facegen");
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var insertionIndex = -1;

        for (int i = 0; i < newInstructions.Count - 1; i++)
        {
            var instruction = newInstructions[i];
            if (instruction.opcode == OpCodes.Newobj && instruction.operand == AccessTools.Constructor(typeof(AgentVisualsData)))
            {
                insertionIndex = i + 1;
                break;
            }
        }

        if (insertionIndex < 0)
        {
            throw new ArgumentException("Cannot find AgentVisualsData constructor. Patch: RefreshCharacterEntityAuxPatch");
        }

        var insertedInstructions = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RefreshCharacterEntityAuxPatch), nameof(GetActionSet))),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(AgentVisualsData), nameof(AgentVisualsData.ActionSet)))
        };

        newInstructions.InsertRange(insertionIndex, insertedInstructions);
        return newInstructions.AsEnumerable();
    }
}
