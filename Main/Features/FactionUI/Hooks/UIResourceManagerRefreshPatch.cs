using System;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: puts the front end's runtime images, fonts and brushes back after the engine
/// rebuilds its UI tables (#704), in place of Kysaro's postfix on <c>RefreshSpriteData</c>. Target: the
/// public static <c>UIResourceManager.Refresh()</c> (v1.5.3 UIResourceManager.cs:45), which replaces the
/// sprite table, the font factory and the brush factory. Its runtime caller,
/// <c>GauntletUISubModule.RefreshResources</c>, runs it between releasing the open movies and rebuilding
/// them (GauntletUISubModule.cs:65-121), so the postfix lands before any themed screen is rebuilt. That
/// happens only when the native side loads a module at runtime (<c>OnNewModuleLoad</c>, :144-146); the
/// first refresh at startup runs before this patch is applied.
/// </summary>
[HarmonyPatch(typeof(UIResourceManager), nameof(UIResourceManager.Refresh))]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class UIResourceManagerRefreshPatch
{
    private static bool _reported;

    static void Postfix()
    {
        try
        {
            FactionUIPatchContext.Sprites?.OnEngineResourcesRefreshed();
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(UIResourceManagerRefreshPatch), ex);
        }
    }
}
