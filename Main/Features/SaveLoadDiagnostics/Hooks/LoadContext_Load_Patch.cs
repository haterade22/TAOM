using System;
using HarmonyLib;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// What a load read from the save's header (plan 114, LOAD-TIME-02): the figures rgl reports as
// [GameData.Read] Object/Container Length, next to the reload counters. Observe only. Own category
// because the three counts are bound by name to private engine fields.
[HarmonyPatch(typeof(LoadContext), nameof(LoadContext.Load), new[] { typeof(LoadData), typeof(bool) })]
[HarmonyPatchCategory(Category)]
public static class LoadContext_Load_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_LoadCounts";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;
    private static AccessTools.FieldRef<LoadContext, int>? _objectCount;
    private static AccessTools.FieldRef<LoadContext, int>? _stringCount;
    private static AccessTools.FieldRef<LoadContext, int>? _containerCount;

    // Installed v1.4.8 LoadContext.cs:11, :13, :15. A field that no longer exists is left null and
    // its count is omitted from the line (never a fake 0).
    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _objectCount = BindCount("_objectCount");
        _stringCount = BindCount("_stringCount");
        _containerCount = BindCount("_containerCount");
    }

    [HarmonyPostfix]
    public static void Postfix(LoadContext __instance)
    {
        var svc = _service;
        if (svc == null) return;
        try
        {
            svc.LogPhase(SaveLoadPhase.LoadCounts, SaveLoadTokens.LoadCounts(
                Read(_objectCount, __instance), Read(_containerCount, __instance), Read(_stringCount, __instance)));
        }
        catch { /* diagnostic only */ }
    }

    private static int? Read(AccessTools.FieldRef<LoadContext, int>? field, LoadContext context) =>
        field == null ? null : field(context);

    private static AccessTools.FieldRef<LoadContext, int>? BindCount(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<LoadContext, int>(name);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[SaveLoad] LoadContext.{name} not bound: its count is left out of LoadCounts ({ex.GetType().Name})");
            return null;
        }
    }
}
