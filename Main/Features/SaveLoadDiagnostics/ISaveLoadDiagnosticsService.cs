using System;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics;

// Always-on save/load lifecycle logging (no MCM toggle — the affected users won't open
// settings, and both paths are cold). Thread-safety contract: every member may be called
// concurrently from TWParallel load workers and the async save-writer thread.
public interface ISaveLoadDiagnosticsService
{
    // Resets seq/clock/fault-throttle/unknown-id dedup, then stamps LoadRequested.
    void BeginLoadAttempt(string saveName);

    // Resets the same lifecycle state, then stamps SaveBegin.
    void BeginSaveAttempt(string saveName);

    void LogPhase(SaveLoadPhase phase, string detail);

    // ERROR-level stamp with the flattened exception chain (Aggregate/TargetInvocation
    // unwrapped) plus the primary stack on a second line. Throttled per attempt so a
    // systematically corrupt graph cannot flood the log from parallel workers.
    void LogFault(SaveLoadPhase phase, string detail, Exception? exception);

    // WARNING-level "this build has no type/container definition for a SaveId present in
    // the save" stamp (definer/build mismatch signature). Deduped per (kind, saveId) per
    // attempt — the engine calls the surrounding hook once per saved object.
    void MarkUnknownSaveId(string kind, string saveId);

    // Faults recorded this attempt (throttle-counted). Lets outcome hooks write an honest
    // message: zero interior stamps means the failure happened in an unhooked parse phase,
    // not "see GraphFault stamps above" pointing at stamps that don't exist.
    int FaultCount { get; }
}
