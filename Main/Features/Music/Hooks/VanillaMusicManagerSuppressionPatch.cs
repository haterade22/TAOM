using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Music.Hooks;

/// <summary>
/// Suppresses vanilla psai (MBMusicManager) music when TAOM's music system is active.
///
/// Vanilla campaign map music runs through MBMusicManager → PsaiCore which occupies
/// TaleWorlds.Engine.Music channels — the same channels TAOM uses for its OGG-based
/// playback. Without suppression, psai immediately reclaims a channel every frame via
/// CampaignMusicHandler.TickCampaignMusic → StartTheme, evicting TAOM's track.
///
/// We suppress at StartTheme (the single psai trigger entry-point) rather than at the
/// channel level, so psai's internal rest timer still runs harmlessly without sound.
/// </summary>
[HarmonyPatchCategory("Patch46_Music")]
public static class MBMusicManager_StartTheme_Patch
{
    private static MethodInfo _cachedTargetMethod;

    public static MethodBase TargetMethod()
    {
        return _cachedTargetMethod ??= AccessTools.Method(
            typeof(MBMusicManager),
            "StartTheme",
            new[] { typeof(MusicTheme), typeof(float), typeof(bool) });
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        return !VanillaMusicManagerSuppressionHelper.IsTaomMusicActive();
    }
}
