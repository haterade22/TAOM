using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Infrastructure;
using TAOM.Features.BannerInjection;
using TAOM.Features.CustomBattles.Muster;
using TAOM.Features.FactionUI.Hooks;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace TAOM.Client.Features.CustomBattles.Muster.Hooks;

/// <summary>
/// Opens the muster screen (movie TaomCustomBattle, <see cref="TcbVM"/>) over the Custom Battle screen when it opens.
/// The vanilla screen (with TAOM's CustomBattleScreen.xml and Patch19 selectors) stays underneath, as the fallback
/// when the muster cannot open (#1293: the muster has no way back to it). Applied with Patch19_CustomBattles.
/// The movie draws TAOM's shared front-end art, which only FrontEndMovieService registers and holds (its LoadMovie
/// patch takes the hold before the movie binds and gives it back after the layer goes), so without that service the
/// old screen stays too.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch19_CustomBattles")]
internal static class CustomBattleScreen_OnInitialize_Patch
{
    private const string MovieName = "TaomCustomBattle";
    private const int LayerOrder = 200;

    private static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("TaleWorlds.MountAndBlade.CustomBattle.CustomBattleScreen"), "OnInitialize");

    [HarmonyPostfix]
    private static void Postfix(ScreenBase __instance)
    {
        try
        {
            if (FactionUIPatchContext.Movies == null)
            {
                CustomBattleMuster.Log("front-end art unavailable, the old screen stays");
                return;
            }
            TcbSprites.Prepare(IoC.Resolve<IPathService>().ModuleRootPath);
            var vm = new TcbVM(KingdomBanners());
            var layer = new GauntletLayer(MovieName, LayerOrder);
            layer.LoadMovie(MovieName, vm);
            layer.InputRestrictions.SetInputRestrictions();
            layer.IsFocusLayer = true;
            __instance.AddLayer(layer);
            ScreenManager.TrySetFocus(layer);
        }
        catch (Exception e) { CustomBattleMuster.Log("screen not opened, the old one stays: " + e); }
    }

    // each army's first banner is its kingdom's (taom_spkingdoms.xml, spkingdoms.xslt); without them, the culture banners
    private static Dictionary<string, string>? KingdomBanners()
    {
        try { return IoC.Resolve<IBannerConfigProvider>().GetCultureKingdomBannerKeys(); }
        catch (Exception e) { CustomBattleMuster.Log("kingdom banners unavailable, the culture banners are used: " + e.Message); return null; }
    }
}
