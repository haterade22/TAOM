using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class HarmonyCorrelationCollector
{
    // Convenience overload — builds the raw StackFrame[] from the exception so callers
    // don't have to duplicate the StackTrace construction. Codex review #46 (2026-05-25)
    // MED-02: prior caller passed only the snapshot list with frames=null, leaving every
    // per-frame patch list empty — the advertised correlation feature was dead code.
    public HarmonyCorrelationSnapshot CollectFromException(Exception? exception, IReadOnlyList<StackFrameSnapshot> stackFrames)
    {
        IReadOnlyList<StackFrame>? raw = null;
        if (exception != null)
        {
            try
            {
                var st = new StackTrace(exception, fNeedFileInfo: false);
                raw = st.GetFrames();
            }
            catch { raw = null; }
        }
        return Collect(stackFrames, raw);
    }

    public HarmonyCorrelationSnapshot Collect(IReadOnlyList<StackFrameSnapshot> stackFrames, IReadOnlyList<StackFrame>? frames = null)
    {
        var patchedMethods = SafeGetAllPatchedMethods();
        var ownerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int totalPatchedMethods = patchedMethods.Count;
        foreach (var method in patchedMethods)
        {
            var info = SafeGetPatchInfo(method);
            if (info == null) continue;
            CountOwners(info.Prefixes, ownerCounts);
            CountOwners(info.Postfixes, ownerCounts);
            CountOwners(info.Transpilers, ownerCounts);
            CountOwners(info.Finalizers, ownerCounts);
        }
        var ownersSummary = ownerCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new HarmonyOwnerSummary(kv.Key, kv.Value))
            .ToList();

        var perFrame = new List<StackFramePatchInfo>(stackFrames.Count);
        for (int i = 0; i < stackFrames.Count; i++)
        {
            var sf = stackFrames[i];
            MethodBase? mb = null;
            if (frames != null && i < frames.Count)
            {
                try { mb = frames[i].GetMethod(); } catch { mb = null; }
            }
            var info = mb != null ? SafeGetPatchInfo(mb) : null;
            var list = new List<HarmonyPatchSnapshot>();
            if (info != null)
            {
                AppendPatches(list, info.Prefixes, "Prefix");
                AppendPatches(list, info.Postfixes, "Postfix");
                AppendPatches(list, info.Transpilers, "Transpiler");
                AppendPatches(list, info.Finalizers, "Finalizer");
            }
            perFrame.Add(new StackFramePatchInfo(
                FrameIndex: i,
                MethodFullName: sf.MethodFullName ?? "(unknown)",
                Patches: list));
        }
        return new HarmonyCorrelationSnapshot(perFrame, ownersSummary, totalPatchedMethods);
    }

    private static void CountOwners(IEnumerable<Patch> patches, Dictionary<string, int> ownerCounts)
    {
        if (patches == null) return;
        foreach (var p in patches)
        {
            var owner = p.owner ?? "(unknown)";
            ownerCounts.TryGetValue(owner, out var c);
            ownerCounts[owner] = c + 1;
        }
    }

    private static void AppendPatches(List<HarmonyPatchSnapshot> sink, IEnumerable<Patch> patches, string typeLabel)
    {
        if (patches == null) return;
        foreach (var p in patches)
        {
            string patchMethod = "?";
            try { patchMethod = p.PatchMethod?.DeclaringType?.FullName + "." + p.PatchMethod?.Name; } catch { }
            sink.Add(new HarmonyPatchSnapshot(
                Owner: p.owner ?? "(unknown)",
                PatchType: typeLabel,
                Priority: SafePriority(p),
                PatchMethodFullName: patchMethod));
        }
    }

    private static int SafePriority(Patch p) { try { return p.priority; } catch { return -1; } }

    private static IReadOnlyList<MethodBase> SafeGetAllPatchedMethods()
    {
        try
        {
            var arr = Harmony.GetAllPatchedMethods()?.ToList();
            return arr ?? new List<MethodBase>();
        }
        catch { return Array.Empty<MethodBase>(); }
    }

    private static Patches? SafeGetPatchInfo(MethodBase method)
    {
        try { return Harmony.GetPatchInfo(method); } catch { return null; }
    }
}
