using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public interface IWarOfTheRingService
{
    WarPhase CurrentPhase { get; }
    bool IsWarOfTheRingActive { get; }
    // WotR Momentum #327 — None until EndWar is called.
    WarOutcome Outcome { get; }
    bool ShouldBlockPeace(string kingdomAId, string kingdomBId);
    void CheckPhaseTransition(float elapsedDays);
    // WotR Momentum #327 — terminal transition: phase → WarEnded, records the victor.
    // Lifts all peace-block layers (they key off CurrentPhase == FullWar). Idempotent:
    // the first non-None outcome wins; later calls no-op.
    void EndWar(WarOutcome outcome);
    // Phase 9b #129 P1 — SyncData hook so behavior can restore phase across save-load.
    void SetPhaseFromSave(WarPhase phase);
    // WotR Momentum #327 — SyncData hook so behavior can restore outcome across save-load.
    void SetOutcomeFromSave(WarOutcome outcome);
    // #764: every campaign start, new or loaded, resets phase and outcome before SyncData restores a save.
    void ResetForNewSession();
    // #772: in FullWar, declares every Phase 2 / Hostile-pair war whose stored stance is not War (idempotent).
    // A pair first linked during Full War is already stored as War and is skipped.
    void ReconcileDeclaredWars();
}
