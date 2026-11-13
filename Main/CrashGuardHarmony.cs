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
/// A guard belongs here only when its own patch method touches no engine member outside a try/catch (engine work
/// goes through a service called inside the try), so the guard cannot itself be the patch that throws a
/// missing-API exception and needs stripping. Patch143 (#1099, PlayerEncounter.DoMeeting, shared with Refuge and
/// Supply Lines) is the first.
/// </summary>
internal static class CrashGuardHarmony
{
    internal const string Id = "TAOM.CrashGuards";
    internal const string MainId = "com.taom.mod";

    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        Patch143_EmptyMeetingGuard.Category,
    };

    internal static bool IsCrashGuard(string category) => Categories.Contains(category);

    internal static string HarmonyIdFor(string category) => IsCrashGuard(category) ? Id : MainId;
}
