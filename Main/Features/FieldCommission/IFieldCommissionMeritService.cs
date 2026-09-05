using System;
using System.Collections.Generic;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

/// <summary>
/// All merit/eligibility/offer-queue bookkeeping for Battlefield Promotions. The entry points
/// (<c>FieldCommissionBehavior</c>, <c>FieldCommissionMissionLogic</c>) convert sealed TaleWorlds
/// types to primitives at the boundary and call this with those primitives only (ADR-002/007).
/// </summary>
public interface IFieldCommissionMeritService
{
    /// <summary>Player-healthy / max(1, enemy-healthy). Never NaN for non-negative int inputs.</summary>
    float ComputeRatio(int playerHealthyCount, int enemyHealthyCount);

    /// <summary>Positive-requirement polarity (NaN in either argument fails the gate — never passes).</summary>
    bool IsBattleEligible(float ratio, float ratioThreshold);

    /// <summary>Starts tracking a new battle window. When <paramref name="eligible"/>, snapshots the
    /// current roster (via the injected adapter) as the set of troop ids kills may be registered
    /// against.</summary>
    void BeginBattle(bool eligible);

    /// <summary>Registers one kill for <paramref name="troopId"/> in the CURRENT battle window.
    /// No-ops when the battle isn't eligible or the troop wasn't in the pre-battle snapshot.</summary>
    void RegisterKill(string troopId);

    /// <summary>
    /// Ends the current battle window. ALWAYS clears in-battle kill tracking. Only banks merit and
    /// queues offers when the window was eligible AND <paramref name="won"/> is true — bug fix (a):
    /// this is the ONLY place merit is banked; queuing an offer here never deducts merit (that
    /// happens only in <see cref="CompleteOffer"/>), so a declined/deferred offer costs nothing.
    /// </summary>
    IReadOnlyList<PendingPromotionOffer> EndBattle(bool won);

    bool HasPendingOffers { get; }

    /// <summary>
    /// Drops every queued-but-unshown offer. The queue is process-lifetime state on a singleton and
    /// is deliberately NOT persisted, so it must be emptied whenever the campaign underneath it
    /// changes — otherwise an offer earned in one save surfaces in the next one loaded in the same
    /// session, promoting a troop the player does not have. Banked merit is untouched.
    /// </summary>
    void ClearPendingOffers();

    bool TryDequeueOffer(out PendingPromotionOffer offer);

    /// <summary>Deducts exactly one MeritThreshold's worth of banked merit for
    /// <paramref name="troopId"/>. Call ONLY after a promotion has actually completed (hero
    /// created) — never for a declined, cancelled, or deferred offer.</summary>
    void CompleteOffer(string troopId);

    /// <summary>
    /// Records that the player said "not yet" to this troop type at its current merit. It must then
    /// earn another full <c>MeritThreshold</c> before it may be offered again. Merit is NOT deducted
    /// — declining costs the soldiers nothing, it only delays the next ask. Without this the offer
    /// re-queues after every won battle, because merit only ever grows and the queue condition is
    /// simply "bank >= threshold".
    /// </summary>
    void RecordDeclinedOffer(string troopId);

    int GetMerit(string troopId);

    /// <summary>Directly adds <paramref name="amount"/> merit to <paramref name="troopId"/>'s
    /// bank — no kill/battle context required. Backs <c>taom.fc_grant_merit</c>; negative or
    /// non-positive amounts are ignored.</summary>
    void GrantMerit(string troopId, int amount);

    /// <summary>Race allow-list + IsHero/PrisonGuard gate. Fails closed on an unresolvable troop id
    /// or an invalid race id (validate-before-lookup — never trusts a fallback race name).</summary>
    bool CanPromote(string troopId);

    void RecordPromotedHero(string heroId);

    IReadOnlyList<string> GetPromotedHeroIds();

    /// <summary>Drops ids that fail <paramref name="isAliveAndValid"/> and returns the surviving
    /// list (already applied to internal state).</summary>
    IReadOnlyList<string> PruneDeadPromotedHeroes(Func<string, bool> isAliveAndValid);

    /// <summary>Drops one id after a dismissal (#540). Without it the removed hero's id lingers in
    /// the persisted list, and in <c>taom.fc_status</c>, until the next load's prune. Unknown,
    /// null and empty ids are no-ops.</summary>
    void ForgetPromotedHero(string heroId);

    Dictionary<string, int> ExportMerits();

    void ImportMerits(Dictionary<string, int> merits);

    Dictionary<string, int> ExportDeclinedMarks();

    void ImportDeclinedMarks(Dictionary<string, int> marks);

    List<string> ExportPromotedHeroIds();

    void ImportPromotedHeroIds(List<string> ids);
}
