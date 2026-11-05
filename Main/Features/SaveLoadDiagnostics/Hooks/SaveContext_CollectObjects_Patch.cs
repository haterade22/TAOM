using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.SaveSystem.Save;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// What a save is about to write (plan 114, SAVE-TIME-01). The engine walks the object graph here;
// these counts are the same figures rgl reports as [GameData.Write] ObjectData/ContainerData Length,
// now next to loadsThisProcess so a reload's growth shows in one log. Observe only.
[HarmonyPatchCategory(Category)]
public static class SaveContext_CollectObjects_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_SaveGraph";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;
    private static AccessTools.FieldRef<SaveContext, List<object>>? _childObjects;
    private static AccessTools.FieldRef<SaveContext, List<object>>? _childContainers;
    private static AccessTools.FieldRef<SaveContext, List<string>>? _strings;

    // Binds the three private lists once (installed v1.4.8 SaveContext.cs:75, :79, :83). A field
    // that no longer exists is left null and its count is omitted from the line (never a fake 0).
    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _childObjects = BindList<object>("_childObjects");
        _childContainers = BindList<object>("_childContainers");
        _strings = BindList<string>("_strings");
    }

    // The no-argument overload: CollectObjects(object) also exists (SaveContext.cs:225).
    internal static MethodBase? ResolveTarget() =>
        AccessTools.Method(typeof(SaveContext), "CollectObjects", Type.EmptyTypes);

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
    {
        var target = ResolveTarget();
        if (target == null)
            _logger?.LogWarning("[SaveLoad] SaveContext.CollectObjects() not found: SaveCollected lines not applied (engine drift?)");
        return target;
    }

    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();

    // Runs on the main thread inside SaveContext.Save, after the object walk and before
    // CollectSaveDatas (SaveContext.cs:342-346), so the lists are complete and stable here.
    [HarmonyPostfix]
    public static void Postfix(SaveContext __instance, long __state)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            long collectMs = (Stopwatch.GetTimestamp() - __state) * 1000 / Stopwatch.Frequency;
            var objects = Read(_childObjects, __instance);
            var containers = Read(_childContainers, __instance);
            var strings = Read(_strings, __instance);
            svc.LogPhase(SaveLoadPhase.SaveCollected,
                SaveLoadTokens.SaveCollected(objects?.Count, containers?.Count, strings?.Count, collectMs));
        }
        catch { /* diagnostic only */ }
    }

    private static List<T>? Read<T>(AccessTools.FieldRef<SaveContext, List<T>>? field, SaveContext context) =>
        field == null ? null : field(context);

    private static AccessTools.FieldRef<SaveContext, List<T>>? BindList<T>(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<SaveContext, List<T>>(name);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[SaveLoad] SaveContext.{name} not bound: its count is left out of SaveCollected ({ex.GetType().Name})");
            return null;
        }
    }
}
