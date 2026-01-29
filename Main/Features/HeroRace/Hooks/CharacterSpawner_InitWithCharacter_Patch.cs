using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Scripts;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterSpawner), "InitWithCharacter")]
public class CharacterSpawner_InitWithCharacter_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CharacterSpawner __instance, CharacterCode characterCode, bool useBodyProperties = false)
    {
        try
        {
            var service = IoC.Resolve<ICharacterSpawnerService>();
            service.InitWithCharacter(__instance, characterCode, useBodyProperties);
        }
        catch (Exception)
        {
            // Prevent game crash if IoC is not configured or service fails
        }
    }
}
