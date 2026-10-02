using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: the startup splash in place of the TaleWorlds-and-partners logo video (#704),
/// from Kysaro's <c>SplashVideo_Patch</c>. Target: <c>VideoPlaybackState.SetStartingParameters(string
/// videoPath, string audioPath, string subtitleFileBasePath, float frameRate, bool canUserSkip)</c>
/// (v1.5.3 VideoPlaybackState.cs:20). The engine plays its splash only when no debugger is attached
/// (Module.cs:781), so a launch under a debugger shows neither.
/// </summary>
[HarmonyPatch(typeof(VideoPlaybackState), nameof(VideoPlaybackState.SetStartingParameters))]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class VideoPlaybackStateSetStartingParametersPatch
{
    private static bool _reported;

    static void Prefix(ref string videoPath, ref string audioPath)
    {
        try
        {
            if (FactionUIPatchContext.Media?.ChooseSplash(videoPath) is not { } choice)
                return;
            videoPath = choice.Video;
            audioPath = choice.Audio;
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(VideoPlaybackStateSetStartingParametersPatch), ex);
        }
    }
}
