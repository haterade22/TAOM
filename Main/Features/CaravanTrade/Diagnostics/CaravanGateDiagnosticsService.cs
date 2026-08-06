namespace TAOM.Features.CaravanTrade.Diagnostics;

/// <summary>
/// Names the reason a caravan is not moving, by replaying the engine's own gate order against a
/// snapshot of public party state.
///
/// Why this is worth its weight: every one of the ways a caravan gets stuck is a silent early-return
/// in <c>CaravansCampaignBehavior.HourlyTickParty</c> (:617-683). Nothing is logged, no event fires,
/// and a parked caravan looks identical whether it is waiting out a wound, sheltering from a battle,
/// frozen by a stale quest flag, or permanently trapped by a zero trade score. Those have four
/// different fixes, so guessing between them is how an economy investigation ends up patching the
/// wrong system — which is exactly what happened when this symptom was first read as "the town has
/// no money".
///
/// The order below mirrors the engine's, with one addition at the top: <c>Ai.IsDisabled</c> is
/// checked first because it blocks the physical exit in
/// <c>MobileParty.CheckExitingSettlementParallel</c> (:4085-4104) regardless of what the caravan
/// behavior decides, so it outranks every decision-side gate.
///
/// Not modeled: the naval-only early-returns (<c>IsInRaftState</c>,
/// <c>DefaultBehavior == MoveToNearestLandOrPort</c>). Naval travel is parked in TAOM (#296), so a
/// naval caravan cannot reach those states in a shipped build. If naval is ever re-enabled, add them
/// alongside <see cref="CaravanGate.InMapEvent"/> — they sit in the same <c>:625</c> guard.
/// </summary>
public class CaravanGateDiagnosticsService
{
    /// <summary>
    /// The engine's pre-roll before the wounded roll: <c>randomFloat &lt; 1f / 3f</c>, skipped
    /// entirely by quest caravans.
    /// </summary>
    private const float NonQuestThinkChance = 1f / 3f;

    public CaravanGateVerdict Evaluate(CaravanGateSnapshot snapshot)
    {
        var woundedFraction = ComputeWoundedFraction(snapshot);
        var leaveChance = LeaveChanceForWounded(woundedFraction);

        // Only the in-fortification branch consults the wounded ladder. On the open map the caravan
        // re-decides through GetDestinationForMobileParty instead, so reporting a wounded penalty
        // there would invent a blocker that does not exist.
        var effectiveLeaveChance = snapshot.IsInFortification ? leaveChance : 1f;

        var verdict = new CaravanGateVerdict
        {
            LeaveChancePerThink = effectiveLeaveChance,
            EffectiveHourlyLeaveChance = snapshot.IsCurrentlyUsedByAQuest
                ? effectiveLeaveChance
                : effectiveLeaveChance * NonQuestThinkChance,
            WoundedFraction = woundedFraction,
        };

        return Describe(verdict, ResolveGate(snapshot, effectiveLeaveChance), snapshot);
    }

    private static CaravanGate ResolveGate(CaravanGateSnapshot snapshot, float leaveChance)
    {
        // The two clauses of the engine's exit guard. Both block the party from physically leaving
        // regardless of what the caravan behavior decides, so they outrank every decision-side gate.
        if (snapshot.IsAiDisabled) return CaravanGate.AiDisabled;
        if (snapshot.IsOnHold) return CaravanGate.HeldInPlace;

        // HourlyTickParty:625 — these bail before any settlement branch, so they apply on the open
        // map as well as inside a town.
        if (snapshot.HasMapEvent) return CaravanGate.InMapEvent;
        if (!snapshot.IsPartyTradeActive) return CaravanGate.TradeInactive;
        if (snapshot.DoNotMakeNewDecisions) return CaravanGate.DecisionsSuppressed;

        if (snapshot.IsInFortification)
        {
            // :631 — a naval caravan escapes the siege clause while the blockade is inactive. That
            // asymmetry is the engine's, not ours.
            var siegeHolds = snapshot.IsCurrentSettlementUnderSiege
                && !(snapshot.HasNavalNavigationCapability && !snapshot.IsBlockadeActive);
            if (siegeHolds) return CaravanGate.HoldingForSiege;

            if (snapshot.IsFleeing) return CaravanGate.Fleeing;
            if (snapshot.IsAlerted) return CaravanGate.Alerted;

            if (leaveChance <= 0f) return CaravanGate.WoundedCannotLeave;
        }

        // Checked after the movement gates but before the merely-slow one: a caravan that is both
        // wounded and trapped recovers from the wound on its own, and never recovers from the trap.
        if (IsTrappedByZeroTradeScore(snapshot)) return CaravanGate.NoViableDestination;

        if (snapshot.IsInFortification && leaveChance < 1f) return CaravanGate.WoundedUnlikelyToLeave;

        return CaravanGate.NotBlocked;
    }

    /// <summary>
    /// The engine: <c>TotalManCount &gt; 0 ? (float)TotalWounded / TotalManCount : 1f</c>. The
    /// zero-men branch matters — without it the division yields NaN, and NaN fails every comparison
    /// in the ladder below, so a destroyed caravan would fall through to the healthy 1.0 band and be
    /// reported as free to leave.
    /// </summary>
    private static float ComputeWoundedFraction(CaravanGateSnapshot snapshot) =>
        snapshot.TotalManCount > 0
            ? (float)snapshot.TotalWounded / snapshot.TotalManCount
            : 1f;

    /// <summary>
    /// The wounded ladder from <c>HourlyTickParty</c> :633-659, reproduced exactly — including the
    /// <c>(double)</c> casts.
    ///
    /// Those casts are not cosmetic. The comparisons are written against <c>double</c> literals, so
    /// the <c>float</c> fraction is widened first, and the nearest float to each threshold sits just
    /// ABOVE it (0.4f widens to 0.40000000596…, which beats the double 0.4). The practical effect is
    /// that every band boundary behaves as inclusive rather than exclusive: a caravan at exactly 40%
    /// wounded scores 0 and never leaves. Comparing in <c>float</c> here would get all five
    /// boundaries wrong in the direction that makes stuck caravans look healthy.
    /// </summary>
    private static float LeaveChanceForWounded(float woundedFraction)
    {
        if ((double)woundedFraction > 0.4) return 0f;
        if ((double)woundedFraction > 0.2) return 0.1f;
        if ((double)woundedFraction > 0.1) return 0.2f;
        if ((double)woundedFraction > 0.05) return 0.3f;
        if ((double)woundedFraction > 0.025) return 0.4f;
        return 1f;
    }

    /// <summary>
    /// The permanent trap. <c>GetTradeScoreForTown</c> caps the buy half at
    /// <c>(int)(0.5f * PartyTradeGold)</c>, so under 2 gold that half is 0 for every town in the
    /// world; the sell half needs cargo to score anything. With both at zero every candidate scores
    /// exactly 0, <c>FindNextDestinationForCaravan</c>'s strictly-greater-than-zero test never
    /// passes, <c>ThinkNextDestination</c> returns null, and no AI action is ever set.
    /// </summary>
    private static bool IsTrappedByZeroTradeScore(CaravanGateSnapshot snapshot) =>
        (int)(0.5f * snapshot.PartyTradeGold) <= 0 && snapshot.CargoItemCount <= 0;

    private static CaravanGateVerdict Describe(
        CaravanGateVerdict verdict, CaravanGate gate, CaravanGateSnapshot snapshot)
    {
        verdict.Gate = gate;
        verdict.Explanation = gate switch
        {
            CaravanGate.AiDisabled =>
                "party AI is disabled — it cannot exit a settlement at all, whatever the caravan behavior decides",
            CaravanGate.HeldInPlace =>
                "held in place (ShortTermBehavior == Hold) — usually set when its settlement came under "
                + "siege. It outlives the siege but self-clears on the next re-decide, so give it a few "
                + "hours before treating it as stuck",
            CaravanGate.InMapEvent =>
                "in a map event (battle) — transient, no action needed",
            CaravanGate.TradeInactive =>
                "party trade was never initialized — this caravan is permanently inert and will never trade or move",
            CaravanGate.DecisionsSuppressed =>
                "AI decisions are suppressed by a quest flag — if no quest is active this is a quest-cleanup bug",
            CaravanGate.HoldingForSiege =>
                "holding because its settlement is under siege",
            CaravanGate.Fleeing =>
                "fleeing — it will not re-decide a destination while a threat is in range",
            CaravanGate.Alerted =>
                "alerted by a nearby threat — it re-picks a flee behavior every think and never re-decides, "
                + "so it stays put for as long as enemies are close",
            CaravanGate.WoundedCannotLeave =>
                $"{verdict.WoundedFraction:P1} wounded — at or above 40% the leave chance is exactly zero, "
                + "so it cannot depart until it heals",
            CaravanGate.WoundedUnlikelyToLeave =>
                $"{verdict.WoundedFraction:P1} wounded — it will leave, but slowly "
                + $"({verdict.EffectiveHourlyLeaveChance:P1} per game-hour)",
            CaravanGate.NoViableDestination =>
                $"broke ({snapshot.PartyTradeGold} gold) and carrying nothing — every town scores exactly "
                + "zero, so no destination is ever chosen. Permanently stuck without intervention",
            _ => "not blocked — if it is still parked, the cause is outside the known gates",
        };

        return verdict;
    }
}
