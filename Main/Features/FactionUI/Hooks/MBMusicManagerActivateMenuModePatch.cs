using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: keeps vanilla's menu theme off the main menu while our menu video plays its own
/// audio (#704), from Kysaro's <c>MenuMusic_Activate_Patch</c>. Target: the private
/// <c>MBMusicManager.ActivateMenuMode()</c> (v1.5.3 MBMusicManager.cs:435), whose only caller is
/// <c>Update</c> inside its <c>!_systemPaused</c> branch (:556-566), so skipping it drops no pause gate.
/// Skipping it means <c>PsaiCore.MenuModeEnter</c> never runs; the mode is still set to Menu so
/// <c>Update</c> does not try again every frame.
/// <para>
/// Kysaro's matching prefix on <c>DeactivateMenuMode</c> is not ported: vanilla's body there is
/// <c>MenuModeLeave(); CurrentMode = Paused;</c> (:445-449), and <c>Logik.MenuModeLeave</c> only
/// returns <c>commandIgnored</c> when psai never entered menu mode, so vanilla already does what that
/// prefix did. <c>FactionUITicker</c> lifts the silence once the player leaves the main menu.
/// </para>
/// </summary>
[HarmonyPatch(typeof(MBMusicManager), "ActivateMenuMode")]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class MBMusicManagerActivateMenuModePatch
{
    private static bool _reported;

    static bool Prefix(MBMusicManager __instance)
    {
        var media = FactionUIPatchContext.Media;
        var music = FactionUIPatchContext.Music;
        var state = FactionUIPatchContext.State;
        if (media == null || music == null || state == null)
            return true;

        try
        {
            if (!media.OnActivateMenuMode(state.IsMainMenuActive()))
                return true;
            music.MarkMenuModeWithoutTheme(__instance);
            return false;
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(MBMusicManagerActivateMenuModePatch), ex);
            return true;
        }
    }
}
