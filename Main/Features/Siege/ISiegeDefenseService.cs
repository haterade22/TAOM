using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.Siege.Models;

namespace TAOM.Features.Siege;

public interface ISiegeDefenseService
{
    void OnSiegeStarted(ISiegeEventAdapter siege);

    /// <summary>
    /// Expires overdue events, then grants the defence reward if the player is standing in a
    /// settlement with a live siege they accepted.
    ///
    /// **Authority only under co-op**, as a whole. A 2026-08-01 attempt to split this — shared sweep
    /// host-only, reward on every peer — was reverted, because the reward half is not actually
    /// per-peer: <c>PlayerAccepted</c> and <c>RewardClaimed</c> are fields on the shared
    /// <c>_activeEvents</c> entries, serialised into <c>_taom_siege_active_events</c>, and a joining
    /// client's baseline for that key IS the host's save. So a client would inherit the HOST's
    /// acceptance and either claim a reward it never earned or be blocked by a claim the host
    /// already made. Making the client's reward correct requires per-peer accept/claim state, which
    /// is a feature-level change, not a gate placement. Until then a co-op client gets no siege
    /// reward — a known limitation recorded in <c>docs/features/coop-interop.md</c>.
    /// </summary>
    /// <summary>Expires overdue events and prunes the save-backed <c>_activeEvents</c>. Authority only.</summary>
    void OnHourlyTickShared();

    /// <summary>
    /// Grants the defence reward to the hero THIS peer plays. Runs on every peer.
    ///
    /// A co-op CLIENT records its claim in non-persisted process state rather than in
    /// <c>evt.RewardClaimed</c>, because that field is serialized by <c>SnapshotForSave</c> and
    /// belongs to the host's save record. Without that split a client granting its own reward would
    /// mutate shared saved state (Codex review 2026-08-01, HIGH).
    /// </summary>
    void OnHourlyTickLocalPlayer();

    void OnSiegeEnded(string settlementId);
    bool IsWatchedSiege(ISiegeEventAdapter siege);
    IReadOnlyDictionary<string, ActiveSiegeDefenseEvent> ActiveEvents { get; }

    // Phase 9b #132 — R1 singleton reset for new-campaign-same-process safety
    void Reset();

    // Phase 9b #132 — flat-primitive serialization (mirrors CareerPersistenceBehavior pattern,
    // avoids SaveableTypeDefiner). Each event encoded as "defenderFactionId|deadlineTicks|accepted|rewardClaimed".
    Dictionary<string, string> SnapshotForSave();
    void RestoreFromSave(Dictionary<string, string> snapshot);
}
