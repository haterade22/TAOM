// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using TAOM.Features.MissionStartGuard.Models;

namespace TAOM.Features.MissionStartGuard;

/// <summary>
/// What Patch103's call-site helpers ask: whether to survive a throw, and where to report it
/// (docs/features/mission-start-guard.md). Every member is safe to call from inside a catch block: none throws.
/// </summary>
public interface IMissionStartGuardService
{
    /// <summary>False when the MCM toggle is off, or for <see cref="OutOfMemoryException"/>, which is never swallowed.</summary>
    bool ShouldSurvive(Exception exception);

    /// <summary>Called by Patch103's prefix as <c>Mission.AfterStart</c> begins; a new mission token resets the per-mission counts.</summary>
    void BeginMission(object? missionToken);

    /// <summary>
    /// Records a survived throw: an ERROR line for each of the first 10 per mission (then one WARNING that the rest are
    /// counted, not logged), and at most three on-screen messages per mission.
    /// </summary>
    void Report(StartCall call, string ownerType, string ownerAssembly, Exception exception);

    /// <summary>
    /// Called by Patch103's finalizer when <c>Mission.AfterStart</c> ends. <paramref name="prefixRan"/> is this call's
    /// own flag from Harmony's <c>__state</c>; <paramref name="liveSwaps"/> is the transpiler's latest swap count. Writes
    /// the per-mission summary when something was caught, and one WARNING per process when the guard has been lost: the
    /// prefix did not run for this call (PatchShield strips an owner's prefix, postfix and transpiler together, never its
    /// finalizers), or the live swap count is not all six sites (a later Harmony rebuild soft-failed).
    /// </summary>
    void EndMissionStart(bool prefixRan, int liveSwaps);

    /// <summary>An exception that still left <c>Mission.AfterStart</c> (from the engine calls the guard does not wrap): logged at most three times per process.</summary>
    void ReportEscaped(Exception exception);

    /// <summary>
    /// One <c>EarlyStart</c>, <c>AfterStart</c> or <c>AfterMissionStart</c> call changed the list the engine was walking
    /// around it (#1028). <c>Mission.AfterStart</c> walks <c>MissionBehaviors</c> and <c>MissionObjects</c> with
    /// <c>foreach</c>, so any change (an add, a remove, an indexer write, a sort) makes the engine's next step throw
    /// "Collection was modified" outside every call the guard wraps. Logs the list, the call, the owner, both sizes and,
    /// when the list grew, its newest item ("n/a" otherwise); at most five lines per mission.
    /// </summary>
    void ReportListChange(StartCall call, string ownerType, string ownerAssembly, string list, int before, int after, string newestType);

    /// <summary>Whether a behaviour added while the engine walks <c>MissionBehaviors</c> is queued until the walk ends (#1028). The same toggle as <see cref="ShouldSurvive"/>.</summary>
    bool ShouldDeferBehaviorAdds();

    /// <summary>One INFO line: a behaviour of <paramref name="addedType"/> was queued during <paramref name="call"/> of <paramref name="ownerType"/>.</summary>
    void ReportDeferredAdd(StartCall call, string ownerType, string addedType);

    /// <summary>The install line: how many call sites the transpiler wrapped, or the warning when it is not all of them (which also uses up the once-per-process lost-guard warning).</summary>
    void LogInstall(int wrappedSites);
}
