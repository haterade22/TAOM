using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public interface IWarOfTheRingService
{
    WarPhase CurrentPhase { get; }
    bool IsWarOfTheRingActive { get; }
    // Whether the War of the Ring is switched on: MCM when available, else the JSON. Phase-independent
    // (IsWarOfTheRingActive is FullWar only).
    bool IsEnabled { get; }
    // What a new campaign gets before it pins its own choice: MCM when available, else the JSON. No Test Mode,
    // no campaign pin. The start-options screen, the pin and the mismatch check read these two.
    bool NewCampaignEnabled { get; }
    (int Phase1Day, int Phase2Day) NewCampaignPhaseDays { get; }
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
    // #765: the clamped trigger day CheckPhaseTransition compares against for IsengardWar or FullWar, read
    // live from MCM, Test Mode or the JSON; null for any other phase. It is not the day the phase was
    // entered: a war switched on late, a lowered phase day or Test Mode enters the phase after this day,
    // and a phase day raised after the phase began moves it. The War Chronicle therefore anchors on the
    // later of this day and the day it first saw the phase reached (ChronicleService.PhaseDay).
    int? GetEffectivePhaseDay(WarPhase phase);

    // War declarations follow the phase. Checked in order: a Permanent pair is refused; with the War of
    // the Ring on and WarEnded, nothing else is; a same-side pair is refused (SameAlignment) in every
    // phase before WarEnded; then Peace refuses every war and IsengardWar every pair Phase1.Wars does
    // not list (either order). With the War of the Ring off (JSON or MCM) the answer is IsWarAllowed's.
    // The script's own declarations pass this gate with no exemption: the phase is set before it declares.
    // causedByPlayer: a war the PLAYER causes by a hostile act (DeclareWarAction CausedByPlayerHostility,
    // CausedByCrimeRatingChange or CausedByKingdomCreation) may start before the script allows: the two
    // phase vetoes are lifted and the lore vetoes (Permanent, SameAlignment) stay. False is the AI answer.
    WarVeto GetWarVeto(string kingdomAId, string kingdomBId, bool causedByPlayer = false);
    // How far to push an AI kingdom's score for declaring this war down, so it never proposes (and pays
    // influence for) a war GetWarVeto refuses. 0 when the war is allowed; PhaseVetoScorePenalty for a
    // phase veto (a constant offset, so the order among phase-vetoed neighbours survives);
    // LoreVetoScorePenalty, always larger, for Permanent and SameAlignment (a same-side pair in WarEnded is
    // allowed, so it gets 0).
    float GetDeclareWarScorePenalty(string kingdomAId, string kingdomBId);
    // Peace and IsengardWar only, with the War of the Ring on: makes peace in every war GetWarVeto
    // refuses, so a save or a mid-campaign install cannot keep wars the phase forbids. Returns the
    // number ended. A war involving the player's kingdom is judged as the player's own (causedByPlayer),
    // so one that only a phase veto forbids is kept: those come from the player's own acts, the
    // Mercenary start or an old save. Run it on the daily tick, never at session launch (a MakePeace
    // before the map and siege events finish loading can disturb them), and after CheckPhaseTransition:
    // an install on a late day loads in Peace and jumps to FullWar inside that call, and reconciling
    // first would end every war.
    int EndWarsForbiddenByPhase();
}
