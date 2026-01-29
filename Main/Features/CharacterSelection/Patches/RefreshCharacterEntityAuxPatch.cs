using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;

namespace TAOM.Features.CharacterSelection.Patches;

[HarmonyPatch(typeof(BodyGeneratorView), "RefreshCharacterEntityAux")]
[HarmonyPatchCategory("LatePatches")]
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
        var ctor = AccessTools.Constructor(typeof(AgentVisualsData), Type.EmptyTypes);
        if (ctor == null)
            throw new ArgumentException("Cannot find AgentVisualsData parameterless constructor. Patch: RefreshCharacterEntityAuxPatch");

        var actionSetMethod = typeof(AgentVisualsData).GetMethod(nameof(AgentVisualsData.ActionSet));
        if (actionSetMethod == null)
            throw new ArgumentException("Cannot find AgentVisualsData.ActionSet method. Patch: RefreshCharacterEntityAuxPatch");

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
            throw new ArgumentException("Cannot find AgentVisualsData Newobj in IL. Patch: RefreshCharacterEntityAuxPatch");

        newInstructions.InsertRange(insertionIndex, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RefreshCharacterEntityAuxPatch), nameof(GetActionSet))),
            new CodeInstruction(OpCodes.Callvirt, actionSetMethod)
        });

        return newInstructions.AsEnumerable();
    }
}
