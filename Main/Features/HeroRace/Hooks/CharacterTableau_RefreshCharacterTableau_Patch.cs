using HarmonyLib;
using System;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;
using FaceGen = TaleWorlds.Core.FaceGen;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), "FirstTimeInit")]
[HarmonyPatchCategory("Patch1_FirstTimeInit")]
public class CharacterTableau_FirstTimeInit_Patch
{
    public static RacePositionConfig Config;

    [HarmonyPostfix]
    public static void Postfix()
    {
        try
        {
            Config = RacePositionConfig.LoadConfig("CharacterAvatarPatch");
        }
        catch (Exception)
        {
            // Prevent game crash during mod initialization
        }
    }
}

[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("Patch2_RefreshTableau")]
public class CharacterTableau_RefreshCharacterTableau_Patch
{
    [HarmonyPrefix]
    public static void Prefix(ref AgentVisuals ____oldAgentVisuals, int ____race, bool ____isFemale)
    {
        try
        {
            if (____oldAgentVisuals == null || ____race < 0)
                return;

            var monster = FaceGen.GetBaseMonsterFromRace(____race);
            if (monster == null)
                return;

            string isFemaleText = ____isFemale ? "_female_" : "";
            var actionSet = MBGlobals.GetActionSet($"as_{monster.StringId}{isFemaleText}_warrior");

            var newData = ____oldAgentVisuals.GetCopyAgentVisualsData();
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
