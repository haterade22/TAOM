using System;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.Music.Hooks;

internal static class CharacterCreationAmbientSuppressor
{
    private static MethodInfo _cachedStopSoundMethod;

    internal static MethodInfo ResolveStopSoundMethod()
    {
        var screenType = AccessTools.TypeByName("SandBox.View.CharacterCreation.CharacterCreationScreen");
        return screenType == null
            ? null
            : AccessTools.Method(screenType, "StopSound", Type.EmptyTypes);
    }

    internal static bool Suppress(object screen)
    {
        if (screen == null)
            return false;

        try
        {
            var stopSound = _cachedStopSoundMethod ??= ResolveStopSoundMethod();
            if (stopSound == null)
                return false;

            stopSound.Invoke(screen, null);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
