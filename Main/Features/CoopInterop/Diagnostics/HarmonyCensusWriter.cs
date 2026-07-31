using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop.Diagnostics;

/// <summary>
/// Thin entry point for the Harmony census: walks HarmonyLib's public runtime registry, projects it
/// onto <see cref="HarmonyCensusMethod"/>, and hands it to <see cref="HarmonyCensusReportBuilder"/>.
/// All analysis lives in the builder.
///
/// Only three public HarmonyLib calls are used — <c>GetAllPatchedMethods</c>, <c>GetPatchInfo</c>,
/// and the <c>Patch.owner</c>/<c>PatchMethod</c> metadata already read by
/// <c>HarmonyCorrelationCollector</c>. Nothing here reads a method body, IL, or any third-party
/// assembly's implementation.
/// </summary>
public static class HarmonyCensusWriter
{
    public static void Write(
        HarmonyCensusReportBuilder builder,
        IReadOnlyList<string> coopModuleIds,
        IModLogger logger)
    {
        try
        {
            var methods = Collect();
            foreach (var line in builder.BuildReport(methods, coopModuleIds))
                logger.LogInfo(line);

            LogHarmonyInstances(logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[HarmonyCensus] census failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static List<HarmonyCensusMethod> Collect()
    {
        var result = new List<HarmonyCensusMethod>();

        List<MethodBase> patched;
        try { patched = Harmony.GetAllPatchedMethods()?.ToList() ?? new List<MethodBase>(); }
        catch { return result; }

        foreach (var method in patched)
        {
            if (method == null) continue;

            Patches? info;
            try { info = Harmony.GetPatchInfo(method); } catch { continue; }
            if (info == null) continue;

            var patches = new List<HarmonyCensusPatch>();
            Append(patches, info.Prefixes, "Prefix");
            Append(patches, info.Postfixes, "Postfix");
            Append(patches, info.Transpilers, "Transpiler");
            Append(patches, info.Finalizers, "Finalizer");
            // Lib.Harmony 2.4.2's Patches type exposes SIX collections, not four. An owner whose
            // only patch on a method is an inner prefix/postfix would otherwise be absent from the
            // census entirely — and a missing owner is exactly the signal this report tells the
            // reader to interpret as "two 0Harmony instances are loaded", sending them after a
            // load-order problem that does not exist.
            Append(patches, info.InnerPrefixes, "InnerPrefix");
            Append(patches, info.InnerPostfixes, "InnerPostfix");
            if (patches.Count == 0) continue;

            string ns, fullName;
            try
            {
                ns = method.DeclaringType?.Namespace ?? string.Empty;
                fullName = $"{method.DeclaringType?.FullName ?? "?"}.{method.Name}";
            }
            catch { ns = string.Empty; fullName = "?"; }

            result.Add(new HarmonyCensusMethod(ns, fullName, patches));
        }

        return result;
    }

    private static void Append(List<HarmonyCensusPatch> sink, IEnumerable<Patch>? patches, string kind)
    {
        if (patches == null) return;
        foreach (var patch in patches)
        {
            if (patch == null) continue;
            string owner;
            try { owner = patch.owner; } catch { owner = string.Empty; }
            sink.Add(new HarmonyCensusPatch(owner, kind));
        }
    }

    /// <summary>
    /// Logs every loaded 0Harmony assembly with its path and version.
    ///
    /// More than one means the mods are NOT sharing a patch registry: HarmonyLib's registry is
    /// per-assembly-instance static state, so a census taken from TAOM's copy cannot see patches
    /// made through another copy. If that happens, an owner missing from the report above is not
    /// evidence that the mod patched nothing — it is evidence the census is blind.
    /// </summary>
    private static void LogHarmonyInstances(IModLogger logger)
    {
        try
        {
            var instances = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => string.Equals(SafeName(a), "0Harmony", StringComparison.OrdinalIgnoreCase))
                .Select(a => $"{SafeLocation(a)} v{SafeVersion(a)}")
                .ToList();

            if (instances.Count == 0) return;

            logger.LogInfo($"[HarmonyCensus]   0Harmony instances ({instances.Count}): {string.Join(" | ", instances)}");
            if (instances.Count > 1)
            {
                logger.LogWarning(
                    "[HarmonyCensus]   MULTIPLE 0Harmony assemblies loaded — the patch registry is " +
                    "per-instance, so the owner list above may be missing other mods' patches entirely. " +
                    "Check module load order (TAOM.Dependencies must construct first).");
            }
        }
        catch { /* diagnostics only */ }
    }

    private static string SafeName(Assembly a) { try { return a.GetName().Name ?? "?"; } catch { return "?"; } }
    private static string SafeVersion(Assembly a) { try { return a.GetName().Version?.ToString() ?? "?"; } catch { return "?"; } }
    private static string SafeLocation(Assembly a) { try { return string.IsNullOrEmpty(a.Location) ? "(dynamic)" : a.Location; } catch { return "?"; } }
}
