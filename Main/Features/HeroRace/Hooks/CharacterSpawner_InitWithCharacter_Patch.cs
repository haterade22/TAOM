using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Scripts;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterSpawner), "InitWithCharacter")]
[HarmonyPatchCategory("Patch4_CharacterSpawner")]
public class CharacterSpawner_InitWithCharacter_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterSpawner __instance, CharacterCode characterCode, bool useBodyProperties = false)
    {
        try
        {
            if (characterCode.Race <= 0)
            {
                return true;
            }

            var service = IoC.Resolve<ICharacterSpawnerService>();
            service.InitWithCharacter(__instance, characterCode, useBodyProperties);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
