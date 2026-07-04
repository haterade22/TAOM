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
}
