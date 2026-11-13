using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Features.MissionStartGuard.Models;

namespace TAOM.Features.MissionStartGuard;

/// <summary>
/// The #1028 [Check] ids that tools/ingame_checks/1028.toml reads. A queued add that the flush applied is a pass; one the
/// engine's add threw on is a fail; a change to <c>MissionBehaviors</c> inside the walk that still happened (the loop #1028
/// describes) is a fail. Both write once per mission (the key), at most a few times a mission, on the main thread
/// (csharp-architecture.md "Outcome cost").
/// </summary>
internal static class MissionStartGuardChecks
{
    internal const string DeferredAddId = "taom.mission-start-guard.deferred-add";
    internal const string ListChangeId = "taom.mission-start-guard.list-change";

    internal static void RecordFlush(IOutcomeLog log, string key, int queued, int added)
    {
        if (queued <= 0) return;
        var detail = "queued=" + queued.ToString(CultureInfo.InvariantCulture) + " added=" + added.ToString(CultureInfo.InvariantCulture);
        if (added >= queued)
            log.Pass(DeferredAddId, key, detail);
        else
            log.Fail(DeferredAddId, key, detail + " reason=engine-add-threw");
    }

    internal static void RecordListChange(IOutcomeLog log, string key, StartCall call, string ownerType, string list, int before, int after, string newestType)
    {
        log.Fail(ListChangeId, key, "call=" + call + " owner=" + ownerType + " list=" + list
            + " before=" + before.ToString(CultureInfo.InvariantCulture) + " after=" + after.ToString(CultureInfo.InvariantCulture)
            + " newest=" + newestType);
    }
}
