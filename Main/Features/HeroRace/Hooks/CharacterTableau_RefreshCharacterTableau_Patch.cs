using HarmonyLib;
using System;
using TAOM.Core.Infrastructure;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Tableaus;

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
    public static bool Prefix(CharacterTableau __instance, Equipment oldEquipment = null)
    {
        try
        {
            int race = ReflectionHelper.GetFieldValue<CharacterTableau, int>(__instance, "_race");
            if (race <= 0)
            {
                return true;
            }

            var service = IoC.Resolve<ICharacterTableauService>();
            service.RefreshCharacterTableau(__instance, oldEquipment);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
