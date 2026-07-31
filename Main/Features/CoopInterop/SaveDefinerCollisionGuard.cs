using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Thin entry point for the save-definer collision preflight: walks the loaded assemblies for
/// <c>SaveableTypeDefiner</c> subclasses, reads each one's base save id, and hands the records to
/// <see cref="ISaveDefinerCollisionDetector"/>. All grouping logic lives in the detector.
///
/// Run this BEFORE the engine builds its definition context. The engine performs the identical
/// instantiation moments later and throws an <c>ArgumentException</c> on a duplicate key with a
/// message that names neither mod — so we are not creating a new hazard, only moving it earlier
/// where it can be attributed to two named assemblies.
///
/// Reflection targets verified against installed v1.4.7 (2026-07-31):
/// <c>TaleWorlds.SaveSystem.SaveableTypeDefiner</c> is abstract with a
/// <c>private readonly int _saveBaseId</c> assigned in its protected constructor.
/// </summary>
public static class SaveDefinerCollisionGuard
{
    private const string DefinerTypeName = "TaleWorlds.SaveSystem.SaveableTypeDefiner";
    private const string BaseIdFieldName = "_saveBaseId";

    /// <summary>
    /// Scans and logs. Never throws — a preflight that crashes the game is worse than the crash it
    /// was trying to explain. Returns the collisions found (empty when clean or when the scan
    /// could not run).
    /// </summary>
    public static IReadOnlyList<SaveDefinerCollision> Run(
        ISaveDefinerCollisionDetector detector, IModLogger logger)
    {
        try
        {
            var records = CollectRecords(logger);
            if (records.Count == 0) return Array.Empty<SaveDefinerCollision>();

            var collisions = detector.Detect(records);
            if (collisions.Count == 0)
            {
                logger.LogDebug($"[SaveDefiners] {records.Count} definer(s) found, no base-id collisions.");
                return collisions;
            }

            foreach (var collision in collisions)
            {
                var who = string.Join(", ", collision.Records.Select(r => $"{r.AssemblyName}::{r.TypeName}"));
                if (collision.IsCrossAssembly)
                {
                    logger.LogError(
                        $"[SaveDefiners] SAVE ID COLLISION on base id {collision.BaseId} between: {who}. " +
                        "Two mods claim the same save-system id range; the game will fail to start with " +
                        "an 'item with the same key has already been added' error that names neither mod. " +
                        "Disable one of them.");
                }
                else
                {
                    logger.LogError(
                        $"[SaveDefiners] Duplicate base id {collision.BaseId} within one assembly: {who}. " +
                        "This is a defect in that mod, not a conflict between mods.");
                }
            }
            return collisions;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[SaveDefiners] preflight failed (non-fatal): {ex.Message}");
            return Array.Empty<SaveDefinerCollision>();
        }
    }

    private static List<SaveDefinerRecord> CollectRecords(IModLogger logger)
    {
        var records = new List<SaveDefinerRecord>();

        var definerBase = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => SafeGetType(a, DefinerTypeName))
            .FirstOrDefault(t => t != null);
        if (definerBase == null)
        {
            logger.LogDebug($"[SaveDefiners] {DefinerTypeName} not loaded; preflight skipped.");
            return records;
        }

        var baseIdField = definerBase.GetField(BaseIdFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (baseIdField == null)
        {
            // Engine drift: the field was renamed. Say so rather than silently reporting "no
            // collisions", which would read as a clean bill of health.
            logger.LogWarning(
                $"[SaveDefiners] {DefinerTypeName}.{BaseIdFieldName} not found on the installed engine — " +
                "preflight cannot read base ids and is skipped. Re-verify the field name.");
            return records;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // CollectAssemblyTypesShim makes GetTypes() return the partial list instead of throwing
            // on a ReflectionTypeLoadException, so this walk is safe across broken third-party mods.
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }

            var assemblyName = SafeAssemblyName(assembly);

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || !definerBase.IsAssignableFrom(type)) continue;

                try
                {
                    var instance = Activator.CreateInstance(type, nonPublic: true);
                    if (instance == null) continue;
                    if (baseIdField.GetValue(instance) is int baseId)
                        records.Add(new SaveDefinerRecord(assemblyName, type.FullName ?? type.Name, baseId));
                }
                catch
                {
                    // A definer with a side-effecting or throwing constructor. Skip it — the engine
                    // will hit the same constructor moments later and report it properly.
                }
            }
        }

        return records;
    }

    private static Type? SafeGetType(Assembly assembly, string fullName)
    {
        try { return assembly.GetType(fullName, throwOnError: false); }
        catch { return null; }
    }

    private static string SafeAssemblyName(Assembly assembly)
    {
        try { return assembly.GetName().Name ?? "?"; }
        catch { return "?"; }
    }
}
