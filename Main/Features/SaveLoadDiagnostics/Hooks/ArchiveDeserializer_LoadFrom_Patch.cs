using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// One hook that attributes the raw archive parses (review 2026-07-07 MED): header chunk,
// strings chunk, per-object chunk, per-container chunk all pass through
// ArchiveDeserializer.LoadFrom(byte[]). A bit-flipped/truncated chunk that survived the
// deflate faults here first; the byte length in the stamp distinguishes "tiny/empty chunk"
// (truncation) from "full-size chunk with garbage" (bit corruption). Internal engine type →
// own isolated category.
[HarmonyPatchCategory("Patch61_SaveLoadDiagnostics_ArchiveParse")]
public static class ArchiveDeserializer_LoadFrom_Patch
{
    private const string DeserializerTypeName = "TaleWorlds.SaveSystem.ArchiveDeserializer";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;

    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    internal static Type? ResolveDeserializerType() => AccessTools.TypeByName(DeserializerTypeName);

    public static MethodBase? TargetMethod()
    {
        var type = ResolveDeserializerType();
        if (type == null)
        {
            _logger?.LogWarning("[SaveLoad] ArchiveDeserializer type not found — archive-parse fault attribution not applied (engine drift?)");
            return null;
        }
        var method = AccessTools.Method(type, "LoadFrom", new[] { typeof(byte[]) });
        if (method == null)
            _logger?.LogWarning("[SaveLoad] ArchiveDeserializer.LoadFrom(byte[]) not found — archive-parse fault attribution not applied (engine drift?)");
        return method;
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, byte[]? binaryArchive)
    {
        var svc = _service;
        if (svc == null || __exception == null) return;
        try
        {
            svc.LogFault(SaveLoadPhase.GraphFault,
                $"kind=archiveParse chunkBytes={binaryArchive?.Length ?? -1} — a raw archive chunk failed to parse (truncated or bit-corrupted save data)",
                __exception);
        }
        catch { /* diagnostic only */ }
    }
}
