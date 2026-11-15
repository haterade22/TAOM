using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.Library;

namespace TAOM.Client.Features.Realms.Encyclopedia;

/// <summary>The two hand-applied patches that put <see cref="TitleEncyclopediaPage"/> into the encyclopedia.</summary>
internal static class TitleEncyclopediaPagePatch
{
    private static bool _patched;

    /// <summary>
    /// Adds the page to the encyclopedia. The base constructor reads a page's target types only from EncyclopediaModel, an
    /// attribute this build keeps internal, so a constructor prefix gives them. The page is added to the manager's pages
    /// after it builds them, inside a try/catch: the public OverrideEncyclopediaModel would let the manager create it, but
    /// a throw in its constructor would then reach the engine. Without both patches the encyclopedia has no Titles page.
    /// </summary>
    internal static void Apply()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var harmony = new Harmony("TAOM.Realms.TitlePage");
            _identifierTypes = AccessTools.Field(typeof(EncyclopediaPage), "_identifierTypes");
            _pages = AccessTools.Field(typeof(EncyclopediaManager), "_pages");
            var constructor = AccessTools.Constructor(typeof(EncyclopediaPage), Type.EmptyTypes);
            var create = AccessTools.Method(typeof(EncyclopediaManager), nameof(EncyclopediaManager.CreateEncyclopediaPages));
            if (_identifierTypes == null || _pages == null || constructor == null || create == null)
            {
                Debug.Print("[TAOM.Realms] the encyclopedia page internals were not found; the encyclopedia has no Titles page.");
                return;
            }
            harmony.Patch(constructor, prefix: new HarmonyMethod(typeof(TitleEncyclopediaPagePatch), nameof(PagePrefix)));
            harmony.Patch(create, postfix: new HarmonyMethod(typeof(TitleEncyclopediaPagePatch), nameof(CreatePagesPostfix)));
            _canAdd = true;
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] the encyclopedia Titles page is off: {ex}");
        }
    }

    private static System.Reflection.FieldInfo? _identifierTypes;
    private static System.Reflection.FieldInfo? _pages;
    private static bool _canAdd;

    // Runs before the base constructor's body, which keeps the value when it finds no attribute of its own.
    private static void PagePrefix(EncyclopediaPage __instance)
    {
        if (__instance is TitleEncyclopediaPage)
            _identifierTypes?.SetValue(__instance, new[] { typeof(RealmsTitle) });
    }

    private static void CreatePagesPostfix(EncyclopediaManager __instance)
    {
        if (!_canAdd)
            return;
        try
        {
            if (_pages?.GetValue(__instance) is Dictionary<Type, EncyclopediaPage> pages && !pages.ContainsKey(typeof(RealmsTitle)))
                pages.Add(typeof(RealmsTitle), new TitleEncyclopediaPage());
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] adding the encyclopedia Titles page threw: {ex}");
        }
    }
}
