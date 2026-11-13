using System;
using System.Collections.Generic;
using TAOM.Features.MapEventGuard.Hooks;

namespace TAOM;

/// <summary>
/// Which Harmony id a patch category is applied under. Feature patches use "com.taom.mod". Crash guards that share
/// their target method with another TAOM patch use <see cref="Id"/>: PatchShield strips every unprotected owner's
/// prefixes, postfixes and transpilers on a method after a missing-API exception there, so under the main id a
/// guard would be removed together with the feature patch that broke. "TAOM.CrashGuards" starts with "TAOM", which
/// PatchShieldPolicy.CompiledProtectedOwnerPrefixes protects.
///
/// A guard belongs here only when its patch class names no TaleWorlds member in any method body, not even inside
/// its own try: a member the engine no longer has raises its exception when the JIT compiles the method that names
/// it, before that method's try runs, and a protected owner is never stripped, so the guard would be swallowed on
/// every call. Engine work lives in a service or adapter method that the patch calls inside its try. The types of
/// the patch method's parameters are the signature Harmony binds, and are allowed. The gate is
/// CrashGuardHarmonyTests.CrashGuardPatchClasses_NameNoTaleWorldsMember. Patch143 (#1099, PlayerEncounter.DoMeeting,
/// shared with Refuge and Supply Lines) is the first.
/// </summary>
internal static class CrashGuardHarmony
{
    internal const string Id = "TAOM.CrashGuards";
    internal const string MainId = "com.taom.mod";

    /// <summary>Both ids count as TAOM's own in the dev console's patch report.</summary>
    internal static readonly string[] TaomOwnerIds = { MainId, Id };

    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        Patch143_EmptyMeetingGuard.Category,
    };

    internal static bool IsCrashGuard(string category) => Categories.Contains(category);
}
