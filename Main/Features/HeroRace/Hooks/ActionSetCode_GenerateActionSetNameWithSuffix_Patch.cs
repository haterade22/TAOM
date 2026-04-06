using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(ActionSetCode), "GenerateActionSetNameWithSuffix")]
[HarmonyPatchCategory("Late_ActionSetOverride")]
public class ActionSetCode_GenerateActionSetNameWithSuffix_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref string __result, Monster monster, bool isFemale, string suffix)
    {
        try
        {
            if (monster == null)
            {
                __result = "as_human" + (isFemale ? "_female" : "") + suffix;
                return false;
            }

            // Match vanilla: prefer BaseMonster when present, otherwise use full StringId
            var monsterId = !string.IsNullOrEmpty(monster.BaseMonster)
                ? monster.BaseMonster
                : monster.StringId;

            __result = "as_" + monsterId + (isFemale ? "_female" : "") + suffix;
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
