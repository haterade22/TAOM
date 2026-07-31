using System.Collections.Generic;

namespace TAOM.Features.CoopInterop.Diagnostics;

/// <summary>One patch attached to a method, reduced to owner + kind.</summary>
public sealed record HarmonyCensusPatch(string Owner, string PatchType);

/// <summary>
/// One Harmony-patched method and every patch on it.
///
/// This carries only what HarmonyLib's own public runtime registry reports — owner ids, patch
/// kinds, and reflection METADATA (namespace, type and method names). It deliberately carries no
/// IL, no method bodies and no decompiled content, so the census can never become an analysis of
/// another mod's implementation.
/// </summary>
public sealed record HarmonyCensusMethod(
    string TypeNamespace,
    string MethodFullName,
    IReadOnlyList<HarmonyCensusPatch> Patches);
