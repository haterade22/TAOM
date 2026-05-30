using System;
using System.Xml;
using Bannerlord.UIExtenderEx.ResourceManager;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.Mcm.Hooks;

/// <summary>
/// Patch41 — Postfix on <c>WidgetFactoryManager.CreateAndRegister(string, XmlDocument)</c> (UIExtenderEx).
///
/// MCM registers its embedded options-screen prefabs through this method. The registered
/// <c>Func&lt;WidgetPrefab&gt;</c> closes over the SAME <see cref="XmlDocument"/> reference and parses it
/// lazily at first screen-open, so mutating the document here (before that parse) repairs the
/// inverted layout. See <see cref="McmLayoutRewriter"/> for the root-cause analysis.
///
/// Why a Harmony Postfix instead of UIExtenderEx's <c>[PrefabExtension]</c>: MCM's embedded prefabs
/// are built via UIExtenderEx's <c>LoadFromDocument</c> reverse-patch, which never runs the
/// <c>ProcessMovie</c> step that applies <c>[PrefabExtension]</c> patches — so a PrefabExtension on
/// these movies is a silent no-op. This Postfix sits on MCM's actual load path.
///
/// Timing: registered from <c>SubModule.OnSubModuleLoad</c>, which completes for all modules before
/// MCM's <c>ResourceInjector.Inject()</c> runs at <c>OnBeforeInitialModuleScreenSetAsRoot</c> — so the
/// Postfix is already attached when MCM calls <c>CreateAndRegister</c>.
/// </summary>
[HarmonyPatch(typeof(WidgetFactoryManager), nameof(WidgetFactoryManager.CreateAndRegister),
    new[] { typeof(string), typeof(XmlDocument) })]
[HarmonyPatchCategory("Patch41_McmLayoutFix")]
public static class Patch41_McmLayoutFix
{
    private static IModLogger _logger;
    private static IModLogger Logger => _logger ??= TAOM.IoC.Resolve<IModLogger>();

    [HarmonyPostfix]
    public static void Postfix(string name, XmlDocument xmlDocument)
    {
        try
        {
            int flipped = McmLayoutRewriter.FlipMcmLayout(name, xmlDocument);
            if (flipped > 0)
                Logger.LogInfo($"[McmLayoutFix] Flipped {flipped} VerticalBottomToTop layout(s) in MCM prefab '{name}'.");
        }
        catch (Exception ex)
        {
            // A cosmetic layout fix must never break MCM screen registration — log and swallow.
            try { Logger.LogError($"[McmLayoutFix] Rewrite failed for '{name}': {ex.GetType().Name}: {ex.Message}"); }
            catch { /* logger unavailable — nothing safe to do */ }
        }
    }
}
