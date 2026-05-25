using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

public sealed record HarmonyCorrelationSnapshot(
    IReadOnlyList<StackFramePatchInfo> PatchesPerStackFrame,
    IReadOnlyList<HarmonyOwnerSummary> AllOwnersByPatchCount,
    int TotalPatchedMethods);

public sealed record StackFramePatchInfo(
    int FrameIndex,
    string MethodFullName,
    IReadOnlyList<HarmonyPatchSnapshot> Patches);

public sealed record HarmonyPatchSnapshot(
    string Owner,
    string PatchType,        // Prefix | Postfix | Transpiler | Finalizer
    int Priority,
    string PatchMethodFullName);

public sealed record HarmonyOwnerSummary(
    string Owner,
    int PatchCount);
