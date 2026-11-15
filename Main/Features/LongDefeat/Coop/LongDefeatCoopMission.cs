using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Coop;

/// <summary>What a run's mission takes from its co-op session: Coop's behaviours and the run's co-op side (both empty or null solo).</summary>
internal sealed record LongDefeatCoopParts(IReadOnlyList<MissionBehavior> Session, LongDefeatRunCoop? Run);

/// <summary>How the mission launcher puts a co-op run (#1280) together, kept out of the launcher so it stays a thin entry point.</summary>
internal static class LongDefeatCoopMission
{
    /// <summary>
    /// A solo run (no session id) gets no session behaviours. A co-op run gets Coop's behaviours for the session this member is in, and
    /// only its elected host may take a soldier, once its director starts: a follower's soldiers are the host's. A co-op run leaves by
    /// the solo exit; its mission's end closes the session (<see cref="LongDefeatCoopRuns.MissionEnded"/>).
    /// </summary>
    public static LongDefeatCoopParts For(string? sessionId, LongDefeatCoopRuns runs) =>
        sessionId == null
            ? new LongDefeatCoopParts(Array.Empty<MissionBehavior>(), null)
            : new LongDefeatCoopParts(SessionBehaviors(sessionId, runs.State),
                RunCoop(sessionId, runs, () => Mission.Current?.SetPlayerCanTakeControlOfAnotherAgentWhenDead()));

    /// <summary>
    /// Coop's session behaviours, then the run's own. <c>CreateMissionBehaviors</c> (in <see cref="For"/>) already turned Coop's spawn gate
    /// on, so it is called before <c>MissionState.OpenNew</c>. The list order does not matter to the gate: the engine adds every
    /// <c>MissionLogic</c> before Coop's controller, which is a plain <c>MissionBehavior</c> (MissionState.cs:281-286).
    /// </summary>
    public static IReadOnlyList<MissionBehavior> Compose(IReadOnlyList<MissionBehavior> session, IReadOnlyList<MissionBehavior> own) =>
        session.Concat(own).ToList();

    /// <summary>The run's co-op side: the role Coop elected for this member, the host's result sent to the others, and the mission's end.</summary>
    public static LongDefeatRunCoop RunCoop(string sessionId, LongDefeatCoopRuns runs, Action becameHost) =>
        new(() => runs.State.RoleFor(sessionId), becameHost, result => runs.ReportEnded(sessionId, result), () => runs.MissionEnded(sessionId));

    /// <summary>Coop's behaviours only for the session this member is in; none for another id or without the link.</summary>
    internal static IReadOnlyList<MissionBehavior> SessionBehaviors(string sessionId, LongDefeatCoopState state) =>
        state.Link != null && state.RoleFor(sessionId) != LongDefeatCoopRole.None
            ? state.Link.CreateMissionBehaviors(sessionId)
            : Array.Empty<MissionBehavior>();
}
