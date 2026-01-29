using HarmonyLib;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterTableau), "FirstTimeInit")]
public class CharacterTableau_FirstTimeInit_Patch
{
    public static RacePositionConfig Config;

    public static void Postfix()
    {
        Config = RacePositionConfig.LoadConfig("CharacterAvatarPatch");
    }
}

[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
public class CharacterTableau_RefreshCharacterTableau_Patch
{
    public static void Postfix(CharacterTableau __instance, Equipment oldEquipment = null)
    {
        var service = IoC.Resolve<ICharacterTableauService>();
        service.RefreshCharacterTableau(__instance, oldEquipment);
    }
}
