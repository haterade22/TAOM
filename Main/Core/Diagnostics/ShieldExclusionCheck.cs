using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Core.Diagnostics;

/// <summary>
/// Asks the loaded TAOM.Dependencies.dll whether PatchShield leaves each engine method of a list alone
/// (<c>PatchShieldPolicy.IsExcludedTargetMethod</c>). The load-time stamps and the XML merge fast path patch
/// MBObjectManager and CampaignEventDispatcher methods that must never run under PatchShield's finalizer: it would
/// swallow a missing-API exception into an empty XML type or a half-run campaign dispatch, then strip TAOM's
/// patches from the method for the session. Those exclusions live in TAOM.Dependencies, and a player can pair a
/// newer TAOM.dll with an older TAOM.Dependencies.dll (#371; the public 1.4.8 build logs such a MISMATCH), so the
/// two modules ask before they declare their patch categories. Never throws.
/// </summary>
internal static class ShieldExclusionCheck
{
    /// <summary>Null when the loaded TAOM.Dependencies excludes every target; otherwise why it does not.</summary>
    internal static string? Problem(IReadOnlyList<(string Type, string Method)> targets) =>
        Problem(targets, LoadedPolicyExcludes);

    /// <summary>The same, with the policy question as a parameter so tests can stand in for an older DLL.</summary>
    internal static string? Problem(IReadOnlyList<(string Type, string Method)> targets, Func<string, string, bool> isExcluded)
    {
        foreach (var (type, method) in targets)
        {
            try
            {
                if (!isExcluded(type, method))
                    return "PatchShield in the loaded TAOM.Dependencies.dll does not exclude " + type + "." + method +
                           " (ship TAOM and TAOM.Dependencies from one build, #371)";
            }
            catch (MissingMemberException)
            {
                return "the loaded TAOM.Dependencies.dll has no PatchShieldPolicy.IsExcludedTargetMethod, so it is " +
                       "older than this TAOM.dll (ship TAOM and TAOM.Dependencies from one build, #371)";
            }
            catch (Exception ex)
            {
                return "asking the loaded TAOM.Dependencies.dll about " + type + "." + method + " threw " +
                       ex.GetType().Name + ": " + ex.Message;
            }
        }
        return null;
    }

    // Its own method, never inlined: against an older TAOM.Dependencies.dll the call below fails when this method
    // is compiled, which happens on its first call, inside Problem's try.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LoadedPolicyExcludes(string type, string method) =>
        TAOM.Dependencies.Foundation.PatchShieldPolicy.IsExcludedTargetMethod(type, method);
}
