using System;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The co-op side of one run (#1280), for <see cref="LongDefeatRunHost"/>: this member's role as Coop elected it, what to do when this
/// member is the host and its director starts, where the host's result goes, and what the mission's end does. Null for a solo run.
/// </summary>
internal sealed record LongDefeatRunCoop(Func<LongDefeatCoopRole> Role, Action BecameHost, Action<RunResult> Ended, Action MissionEnded);
