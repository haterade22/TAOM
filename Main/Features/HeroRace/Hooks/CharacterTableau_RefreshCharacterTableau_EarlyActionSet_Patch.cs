using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("LatePatches")]
public class CharacterTableau_RefreshCharacterTableau_EarlyActionSet_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref AgentVisuals ____oldAgentVisuals, int ____race, bool ____isFemale)
    {
        try
        {
            if (____oldAgentVisuals == null || ____race < 0)
                return;

            var newData = ____oldAgentVisuals.GetCopyAgentVisualsData();
            var raceName = TaleWorlds.Core.FaceGen.GetRaceNames()[____race];
            var monster = TaleWorlds.Core.FaceGen.GetMonster(raceName);
            var actionSet = MBGlobals.GetActionSetWithSuffix(monster, ____isFemale, "_warrior");

            newData
                .ActionSet(actionSet)
                .Race(____race)
                .Monster(monster);

            ____oldAgentVisuals.Refresh(false, newData, false);
        }
        catch (Exception)
        {
            // Fall through — vanilla will handle refresh
        }
    }
}
