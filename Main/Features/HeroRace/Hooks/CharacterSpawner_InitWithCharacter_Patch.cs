using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Scripts;

namespace TAOM.Features.HeroRace.Hooks;

[HarmonyPatch(typeof(CharacterSpawner), "InitWithCharacter")]
public class CharacterSpawner_InitWithCharacter_Patch
{
    public static void Postfix(CharacterSpawner __instance, CharacterCode characterCode, bool useBodyProperties = false)
    {
        var service = IoC.Resolve<ICharacterSpawnerService>();
        service.InitWithCharacter(__instance, characterCode, useBodyProperties);
    }
}
