using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Features.MissionPerf;

/// <summary>One Harmony patch method the tick profiler needs in place on its target to measure.</summary>
internal sealed class RequiredHook
{
    internal RequiredHook(string name, MethodBase? target, HarmonyPatchType kind, MethodInfo? patchMethod)
    {
        Name = name;
        Target = target;
        Kind = kind;
        PatchMethod = patchMethod;
    }

    internal string Name { get; }
    internal MethodBase? Target { get; }
    internal HarmonyPatchType Kind { get; }
    internal MethodInfo? PatchMethod { get; }

    /// <summary>Resolves a hook from its patch class's own attributes, so the list names exactly what Harmony
    /// applies for that class. A target or method that does not resolve stays null and reads as missing.</summary>
    internal static RequiredHook Of(string name, Type patchClass, string patchMethodName, HarmonyPatchType kind)
    {
        MethodBase? target = null;
        try
        {
            var info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
            if (info.declaringType != null && info.methodName != null)
                target = AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
        }
        catch (Exception)
        {
            target = null;
        }
        return new RequiredHook(name, target, kind, AccessTools.Method(patchClass, patchMethodName));
    }
}

/// <summary>
/// Which of the hooks the tick profiler needs are in place on their targets right now. The install is a
/// startup fact; Harmony patches are not: another mod's transpiler can remove an anchor a rewrite needs, and
/// PatchShield strips every unprotected owner's patches on a method it rescues, TAOM's own included. This
/// reads <see cref="Harmony.GetPatchInfo(MethodBase)"/> (injected, so it is tested without a game).
/// </summary>
internal static class HookHealth
{
    /// <summary>The names of the hooks that are not in place, in list order; empty when all are. A hook that
    /// cannot be checked (an unresolved target or patch method, or patch info that throws) is named with the
    /// reason, so it never reads as healthy. Patch info is read once per target. Never throws.</summary>
    internal static IReadOnlyList<string> Missing(IReadOnlyList<RequiredHook> hooks, Func<MethodBase, Patches?> patchesOf)
    {
        var missing = new List<string>(hooks.Count);
        var reads = new Dictionary<MethodBase, (Patches? Patches, string? Fault)>();
        foreach (var hook in hooks)
        {
            if (hook.Target == null || hook.PatchMethod == null)
            {
                missing.Add(hook.Name + " (not resolved)");
                continue;
            }

            if (!reads.TryGetValue(hook.Target, out var read))
                reads[hook.Target] = read = Read(hook.Target, patchesOf);
            if (read.Fault != null)
                missing.Add(hook.Name + " (patch info unreadable: " + read.Fault + ")");
            else if (!Has(read.Patches, hook.Kind, hook.PatchMethod))
                missing.Add(hook.Name);
        }
        return missing;
    }

    private static (Patches? Patches, string? Fault) Read(MethodBase target, Func<MethodBase, Patches?> patchesOf)
    {
        try { return (patchesOf(target), null); }
        catch (Exception ex) { return (null, ex.GetType().Name + ": " + ex.Message); }
    }

    private static bool Has(Patches? patches, HarmonyPatchType kind, MethodInfo method)
    {
        var list = patches == null ? null : kind switch
        {
            HarmonyPatchType.Prefix => patches.Prefixes,
            HarmonyPatchType.Postfix => patches.Postfixes,
            HarmonyPatchType.Transpiler => patches.Transpilers,
            HarmonyPatchType.Finalizer => patches.Finalizers,
            _ => null,
        };
        if (list == null)
            return false;
        foreach (var patch in list)
        {
            if (Is(patch, method))
                return true;
        }
        return false;
    }

    // The method itself, or the same metadata token in the same module (a patch Harmony resolved from its token
    // is another reflection object for the same method). A patch whose method cannot be read is not the hook.
    private static bool Is(Patch? patch, MethodInfo wanted)
    {
        try
        {
            var candidate = patch?.PatchMethod;
            return candidate != null
                && (candidate == wanted || (candidate.Module == wanted.Module && candidate.MetadataToken == wanted.MetadataToken));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
