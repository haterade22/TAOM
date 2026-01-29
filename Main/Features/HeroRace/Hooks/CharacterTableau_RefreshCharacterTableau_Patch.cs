using HarmonyLib;
using System;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), "FirstTimeInit")]
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
public class CharacterTableau_RefreshCharacterTableau_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterTableau __instance, Equipment oldEquipment = null)
    {
        try
        {
            var service = IoC.Resolve<ICharacterTableauService>();
            service.RefreshCharacterTableau(__instance, oldEquipment);
        }
        catch (Exception)
        {
            // Prevent game crash if IoC is not configured or service fails
        }
    }
}
