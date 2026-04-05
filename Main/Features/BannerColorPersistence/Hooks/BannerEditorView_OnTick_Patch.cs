using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI.BannerEditor;
using TAOM.Core.Logging;
using TaleWorlds.InputSystem;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(BannerEditorView), nameof(BannerEditorView.OnTick))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class BannerEditorView_OnTick_Patch
{
    private static IBannerColorService? _service;
    private static IModLogger? _logger;
    private static MethodInfo? _refreshMethod;

    public static void Initialize(IBannerColorService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _refreshMethod = typeof(BannerEditorView).GetMethod(
            "RefreshShieldAndCharacter",
            BindingFlags.Instance | BindingFlags.NonPublic);
    }

    [HarmonyPostfix]
    public static void Postfix(BannerEditorView __instance)
    {
        if (!(_service?.IsBannerPasteEnabled() ?? false)) return;
        try
        {
            bool isPaste = __instance.SceneLayer.Input.IsHotKeyPressed("Paste")
                        || __instance.GauntletLayer.Input.IsHotKeyPressed("Paste");
            bool isCopy = __instance.SceneLayer.Input.IsHotKeyPressed("Copy")
                       || __instance.GauntletLayer.Input.IsHotKeyPressed("Copy");

            if (isPaste)
            {
                string clipboardText = Input.GetClipboardText();
                __instance.DataSource.BannerVM.BannerCode = clipboardText;
                _refreshMethod?.Invoke(__instance, null);
            }
            else if (isCopy)
            {
                Input.SetClipboardText(__instance.DataSource.BannerVM.Banner.Serialize());
            }
        }
        catch (System.Exception ex)
        {
            _logger?.LogWarning($"[BannerPaste] {ex.Message}");
        }
    }
}
