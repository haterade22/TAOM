using System;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.Music.Hooks;

[HarmonyPatchCategory("Patch46_Music")]
public static class CharacterCreationScreen_OnFrameTick_MusicPatch
{
    public static MethodBase TargetMethod()
    {
        var screenType = AccessTools.TypeByName("SandBox.View.CharacterCreation.CharacterCreationScreen");
        return screenType == null
            ? null
            : AccessTools.Method(screenType, "OnFrameTick", new[] { typeof(float) });
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance, float __0)
    {
        CharacterCreationMusicScreenPatchHelper.OnFrameTick(__instance, __0);
    }
}

[HarmonyPatchCategory("Patch46_Music")]
public static class CharacterCreationScreen_OnFinalize_MusicPatch
{
    public static MethodBase TargetMethod()
    {
        var screenType = AccessTools.TypeByName("SandBox.View.CharacterCreation.CharacterCreationScreen");
        return screenType == null
            ? null
            : AccessTools.Method(screenType, "TaleWorlds.Core.IGameStateListener.OnFinalize", Type.EmptyTypes);
    }

    [HarmonyPrefix]
    public static void Prefix()
    {
        CharacterCreationMusicScreenPatchHelper.OnFinalize();
    }
}
