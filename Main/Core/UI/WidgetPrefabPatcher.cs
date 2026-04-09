using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace TAOM.Core.UI;

/// <summary>
/// Registers and dispatches prefab modifications.
/// Harmony postfix on WidgetPrefab.LoadFrom() applies registered patches
/// to the fully-built WidgetPrefab before it is cached by WidgetFactory.
/// </summary>
public static class WidgetPrefabPatcher
{
    private static readonly Dictionary<string, List<Action<WidgetPrefab>>> _patches = new();

    public static void Register(string movieName, Action<WidgetPrefab> patchAction)
    {
        if (!_patches.TryGetValue(movieName, out var list))
        {
            list = new List<Action<WidgetPrefab>>();
            _patches[movieName] = list;
        }
        list.Add(patchAction);
    }

    internal static void ApplyPatches(string path, WidgetPrefab prefab)
    {
        var movieName = Path.GetFileNameWithoutExtension(path);
        if (movieName == null || !_patches.TryGetValue(movieName, out var list))
            return;

        foreach (var patch in list)
        {
            try
            {
                patch(prefab);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.Debug.Print(
                    $"[TAOM] WidgetPrefabPatcher: Failed to apply patch for {movieName}: {ex.Message}",
                    0, TaleWorlds.Library.Debug.DebugColor.Red);
            }
        }
    }

    public static void Clear()
    {
        _patches.Clear();
    }
}

[HarmonyPatch(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom))]
[HarmonyPatchCategory("Patch_WidgetPrefabLoader")]
internal static class WidgetPrefab_LoadFrom_Patch
{
    static void Postfix(string path, ref WidgetPrefab __result)
    {
        if (__result != null)
            WidgetPrefabPatcher.ApplyPatches(path, __result);
    }
}
