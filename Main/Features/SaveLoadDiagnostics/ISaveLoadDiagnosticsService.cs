using System;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics;

// Always-on save/load lifecycle logging (no MCM toggle — the affected users won't open
// settings, and both paths are cold). Thread-safety contract: every member may be called
// concurrently from TWParallel load workers and the async save-writer thread.
public interface ISaveLoadDiagnosticsService
{
    // Resets seq/clock/fault-throttle/unknown-id dedup, then stamps LoadRequested. inGame says the
    // player loaded from inside a running game, the suspect path for save growth (plan 114).
    void BeginLoadAttempt(string saveName, bool inGame);

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

    // Load attempts this process, a load cancelled at the module prompt included.
    int LoadsThisProcess { get; }

    // The subset of LoadsThisProcess that started inside a running game.
    int InGameLoadsThisProcess { get; }

    // Loads so far, whether the campaign before the last load was freed, and process memory: the
    // tokens that let one log line be read without the lines before it.
    string ProcessContext();

    // Remembers, weakly, the campaign that just ended, so a later line can say whether it was freed.
    // Takes object because a service never touches a sealed engine type (ADR-007).
    void NoteGameEnded(object endedCampaign);

    // Stores one behavior's save record size. These run before SaveBegin (the engine's OnBeforeSave
    // pass), so BeginSaveAttempt writes the summary and clears them.
    void RecordBehaviorSave(string behaviorId, string behaviorType, BehaviorRecordStats stats, double syncMs);
}
