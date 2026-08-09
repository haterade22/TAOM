using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Features.AutoResolveDiagnostics;

/// <summary>
/// The write half of the auto-resolve diagnostic: record ids, the once-per-session troop census,
/// and putting a finished record on the log.
///
/// Split out of <see cref="AutoResolveDiagnosticsBehavior"/> because that behavior had grown to
/// 237 lines of real state-machine logic — gating, the pending-battle lifecycle, the census latch,
/// id generation and emit policy — against ADR-002's 150-line ceiling for an entry point. Three
/// independent reviews landed on the same observation, and the bugs found in the same pass were in
/// exactly this logic rather than in the event wiring.
///
/// The pending-battle map stays behind in the behavior on purpose: it is keyed by the sealed
/// TaleWorlds <c>MapEvent</c>, which must not cross into a service (ADR-007).
/// </summary>
public interface IAutoResolveLogWriter
{
    /// <summary>
    /// Clears session-scoped state. Called on every session launch so a second campaign in the
    /// same Bannerlord process does not inherit the first one's id sequence — or, the bug this
    /// method exists to prevent, its already-written census latch.
    /// </summary>
    void BeginSession();

    /// <summary>
    /// Dumps the engine's own tier/power/formation/HP for every troop type, once per session.
    /// Turns the offline analyser's assumptions into data that can be checked rather than trusted.
    /// No-op when either the master or the census toggle is off.
    /// </summary>
    void WriteCensus();

    /// <summary>The next record id, unique within a log file.</summary>
    string NextRecordId();

    /// <summary>Renders and writes one record. Null and unrenderable records are dropped.</summary>
    void Emit(BattleLogRecord? record);
}
