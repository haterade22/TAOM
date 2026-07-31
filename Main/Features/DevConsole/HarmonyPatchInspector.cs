using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Reconstructs TAOM's Harmony patch registry at runtime: which <c>[HarmonyPatchCategory]</c>
/// categories the assembly DECLARES, and which of them Harmony actually APPLIED.
///
/// No new bookkeeping was added to <c>SubModule</c> to make this work. The declared set comes from
/// reflecting the assembly for the attribute; the applied set comes from walking
/// <c>Harmony.GetAllPatchedMethods()</c> back to each patch method's declaring type. The safe-walk
/// shape (every engine call wrapped, empty collections on failure) mirrors
/// <c>CrashReport.Collectors.HarmonyCorrelationCollector</c>, which already does this walk in a
/// crash context where throwing is not an option.
/// </summary>
internal static class HarmonyPatchInspector
{
    internal sealed class Result
    {
        internal IReadOnlyList<string> Declared { get; set; } = new string[0];
        internal IReadOnlyDictionary<string, int> AppliedCounts { get; set; } = new Dictionary<string, int>();
        internal IReadOnlyList<string> Uncategorized { get; set; } = new string[0];
        internal IReadOnlyDictionary<string, int> OtherOwners { get; set; } = new Dictionary<string, int>();
        internal int TotalPatchedMethods { get; set; }
    }

    internal static Result Collect(string taomOwnerId, Assembly taomAssembly)
    {
        var result = new Result();
        var types = SafeGetTypes(taomAssembly);

        result.Declared = types
            .Select(SafeGetCategory)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList();

        var applied = new Dictionary<string, int>();
        var uncategorized = new List<string>();
        var otherOwners = new Dictionary<string, int>();

        var patchedMethods = SafeGetAllPatchedMethods();
        result.TotalPatchedMethods = patchedMethods.Count;

        foreach (var method in patchedMethods)
        {
            var info = SafeGetPatchInfo(method);
            if (info == null) continue;

            foreach (var patch in AllPatches(info))
            {
                var owner = SafeOwner(patch);
                if (owner != taomOwnerId)
                {
                    otherOwners.TryGetValue(owner, out var n);
                    otherOwners[owner] = n + 1;
                    continue;
                }

                var declaringType = SafeDeclaringType(patch);
                var category = declaringType == null ? null : SafeGetCategory(declaringType);

                if (string.IsNullOrEmpty(category))
                {
                    // Manual patches carry no category attribute by design — a separate bucket, not a
                    // missing category.
                    uncategorized.Add(SafePatchLabel(patch));
                    continue;
                }

                applied.TryGetValue(category, out var count);
                applied[category] = count + 1;
            }
        }

        result.AppliedCounts = applied;
        result.Uncategorized = uncategorized.Distinct().ToList();
        result.OtherOwners = otherOwners;
        return result;
    }

    private static IEnumerable<Patch> AllPatches(Patches info)
    {
        // Harmony exposes these as ReadOnlyCollection<Patch>; Concat over an empty sequence keeps a
        // null collection from taking the walk down.
        return Empty(info.Prefixes)
            .Concat(Empty(info.Postfixes))
            .Concat(Empty(info.Transpilers))
            .Concat(Empty(info.Finalizers));
    }

    private static IEnumerable<Patch> Empty(IEnumerable<Patch> patches) => patches ?? Enumerable.Empty<Patch>();

    // Mirrors the engine's GetTypesSafe(): a hard GetTypes() throws on the UI types that fail to load
    // outside the game host, which would take the whole report down.
    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray(); }
        catch { return new Type[0]; }
    }

    private static string SafeGetCategory(Type type)
    {
        try
        {
            var attributes = type.GetCustomAttributes(typeof(HarmonyPatchCategory), false);
            return attributes.Length == 0 ? null : ((HarmonyPatchCategory)attributes[0]).info?.category;
        }
        catch { return null; }
    }

    private static IReadOnlyList<MethodBase> SafeGetAllPatchedMethods()
    {
        try { return Harmony.GetAllPatchedMethods()?.ToList() ?? new List<MethodBase>(); }
        catch { return new List<MethodBase>(); }
    }

    private static Patches SafeGetPatchInfo(MethodBase method)
    {
        try { return Harmony.GetPatchInfo(method); } catch { return null; }
    }

    private static string SafeOwner(Patch patch)
    {
        try { return patch.owner ?? "(unknown)"; } catch { return "(unknown)"; }
    }

    private static Type SafeDeclaringType(Patch patch)
    {
        try { return patch.PatchMethod?.DeclaringType; } catch { return null; }
    }

    private static string SafePatchLabel(Patch patch)
    {
        try { return $"{patch.PatchMethod?.DeclaringType?.Name}.{patch.PatchMethod?.Name}"; }
        catch { return "(unreadable patch method)"; }
    }
}
