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

            var monsterId = monster.StringId;
            if (monsterId.Contains("_"))
                monsterId = monsterId.Split('_')[0];

            __result = "as_" + monsterId + (isFemale ? "_female" : "") + suffix;
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
