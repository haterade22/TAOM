using System;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.UI;

namespace TAOM.Features.EquipPresets.Hooks;

/// <summary>
/// Postfix on <c>GauntletInventoryScreen.OnInitialize</c>: creates a new <c>GauntletLayer</c>
/// hosting <c>PresetsOverlay.xml</c> + a <see cref="PresetsOverlayVM"/> datasource.
/// Prefix on <c>OnFinalize</c>: removes the layer and clears the captured VM.
///
/// Z-order: <b>1000</b> — way above vanilla's <c>InventoryScreen</c> layer (z-order 15, verified
/// via decompiled <c>GauntletInventoryScreen.OnInitialize</c> line 135). Documented in the feature
/// doc per the <c>codex review #34</c> z-order convention for SiegeDismount-class overlay features.
///
/// Static <c>_layer</c> + <c>_overlayVm</c> survive across screens within a single Bannerlord
/// process — the OnFinalize prefix nulls both. A leaked _layer is treated as a defensive log
/// message; we don't try to dispose it from the new init (the previous screen's layer is
/// already gone with its parent ScreenBase).
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch33_EquipPresets")]
public static class Patch33_GauntletInventoryScreen
{
    private const int OverlayLayerZOrder = 1000;
    private const string PrefabName = "PresetsOverlay";

    private static GauntletLayer? _layer;
    private static PresetsOverlayVM? _overlayVm;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GauntletInventoryScreen), "OnInitialize")]
    public static void OnInitialize_Postfix(GauntletInventoryScreen __instance)
    {
        try
        {
            // Settings gate: skip overlay creation when the feature is disabled.
            var settings = IoC.Resolve<IEquipPresetsSettingsProvider>();
            if (!settings.IsEnabled) return;

            if (_layer != null || _overlayVm != null)
            {
                IoC.Resolve<IModLogger>().LogWarning("[EquipPresets] OnInitialize: previous overlay state was non-null on entry — leaked teardown");
                _layer = null;
                _overlayVm = null;
            }

            _overlayVm = new PresetsOverlayVM(
                IoC.Resolve<IEquipmentPresetService>(),
                IoC.Resolve<IInventoryScreenAdapter>(),
                settings,
                IoC.Resolve<IModLogger>());

            // GauntletLayer(string name, int localOrder, bool shouldClear) — verified ilspy 2026-05-06.
            _layer = new GauntletLayer("GauntletLayer", OverlayLayerZOrder, true);
            // Required: without InputRestrictions the layer paints but never registers with the
            // screen's input dispatcher — mouse events pass through and the button click is a
            // silent no-op. Do NOT set IsFocusLayer (would steal Esc/Tab/hotkey focus from the
            // live inventory). Parent widget in PresetsOverlay.xml is DoNotAcceptEvents="true" so
            // non-button areas still pass clicks through to vanilla.
            _layer.InputRestrictions.SetInputRestrictions();
            _layer.LoadMovie(PrefabName, _overlayVm);
            __instance.AddLayer(_layer);
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[EquipPresets] overlay creation failed: {ex.Message}"); }
            catch { /* logger unresolvable */ }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GauntletInventoryScreen), "OnFinalize")]
    public static void OnFinalize_Prefix(GauntletInventoryScreen __instance)
    {
        try
        {
            if (_layer != null)
            {
                // Mirror GauntletCareerScreen / GauntletFiefManagementScreen teardown: release
                // the input mask we set in OnInitialize before removing the layer. ScreenBase's
                // HandleFinalize does not reset InputRestrictions itself.
                _layer.InputRestrictions.ResetInputRestrictions();
                __instance.RemoveLayer(_layer);
                _layer = null;
            }
            _overlayVm = null;

            // Clear the captured SPInventoryVM in the screen adapter — the next inventory open
            // will repopulate it via Patch33_SPInventoryVMRefresh. Goes through the interface
            // contract (Clear() is on IInventoryScreenAdapter) so no concrete-cast bypass.
            IoC.Resolve<IInventoryScreenAdapter>()?.Clear();
        }
        catch (Exception ex)
        {
            try { IoC.Resolve<IModLogger>().LogError($"[EquipPresets] overlay teardown failed: {ex.Message}"); }
            catch { /* logger unresolvable */ }
        }
    }
}
