namespace TAOM.Features.CaravanTrade;

/// <summary>
/// How AI/player caravans should treat the war state when choosing a trade town.
/// TAOM is endless-war-by-default (Free-vs-Evil), so the vanilla "no trade with any
/// faction you're at war with" veto collapses each caravan's reachable set to its own
/// side and forces the local shuttle. This policy relaxes that veto.
/// </summary>
public enum WarTradePolicy
{
    /// <summary>Vanilla behavior — war blocks trade. The feature makes no war-gate change.</summary>
    None,

    /// <summary>Lift the war veto entirely — caravans trade at any non-besieged town regardless of war.</summary>
    IgnoreWar,

    /// <summary>
    /// Lift the war veto only between non-enemy alignments — same side (Free↔Free, Evil↔Evil) or
    /// any pairing involving a Neutral faction. A Free caravan reaches other Free/neutral towns but
    /// not Evil towns. Default. Each side resolves via <see cref="Execution.IAlignmentService.GetKingdomSide"/>
    /// (culture-fallback for player-founded kingdoms) with an explicit Neutral-trades-anyone branch —
    /// deliberately NOT <c>AreEnemyAlignments</c>, whose Neutral-as-enemy-of-everyone semantics are inverted here.
    /// </summary>
    SameAlignmentAndNeutral,
}

/// <summary>
/// Pure decision surface for the CaravanTrade feature. No TaleWorlds types cross this boundary —
/// the Harmony postfixes and the caravan GameModel extract primitives and delegate here (ADR-002/007).
/// Every method short-circuits to the vanilla value when the feature is disabled (or when it's a
/// player caravan and player-scoping is off), so master-off restores exact vanilla behavior.
/// </summary>
public interface ICaravanTradeService
{
    /// <summary>
    /// Re-weight vanilla's trade-destination score to stop the closest-town-always-wins shuttle and
    /// make longer viable trips competitive. Strips vanilla's land <c>1/days</c> distance spike and
    /// re-applies a gentler <c>1/(nearFieldFlatten + days)^decayExponent</c> curve, clamped by
    /// <c>maxCompensation</c>; near-equal-distance towns become near-tied so the built-in profit
    /// estimate (which passes through untouched) decides. Then applies the per-caravan recency penalty
    /// so just-visited towns are deprioritized. Naval passes through unchanged (different vanilla
    /// distance factor). The home settlement is compressed like any other town unless
    /// <see cref="ICaravanTradeSettingsProvider.HomeDistanceReweight"/> is off (escape hatch); vanilla's
    /// upstream home-gravity (<c>num5</c>, already folded into <paramref name="rawScore"/>) is preserved
    /// either way, so caravans still return home to deliver payouts on the natural cadence.
    /// </summary>
    /// <param name="rawScore">Vanilla's <c>GetTradeScoreForTown</c> result. Values ≤ 0 (rejections) pass through.</param>
    /// <param name="days">Raw travel time in days (vanilla's <c>num</c>), recomputed from the same public inputs.</param>
    /// <param name="isNaval">Caravan has naval capability (uses vanilla's different naval distance factor).</param>
    /// <param name="isHomeTown">Candidate is the caravan's home settlement (distance re-weight gated by the escape hatch).</param>
    /// <param name="recencyPenaltyFactor">Recency multiplier in (0,1] from <see cref="ICaravanVisitMemory"/>; 1 = no penalty. NaN/out-of-range is ignored.</param>
    /// <param name="isPlayerCaravan">Caravan is player-owned (scoped off when player-application is disabled).</param>
    float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan);

    /// <summary>
    /// Scale the vanilla "very far" distance ceiling so profitable distant towns aren't hard-rejected.
    /// The vanilla cache is a single shared field (not per-caravan), so this is applied globally when
    /// the feature is enabled — it only widens the candidate set; the re-weight and war gate remain
    /// player-scoped. Returns the vanilla value unchanged when disabled.
    /// </summary>
    float ScaleVeryFarDistance(float vanillaVeryFarDays);

    /// <summary>
    /// Whether to lift the vanilla war veto for this caravan→town faction pairing. Returns
    /// <c>false</c> to keep the vanilla veto (the caller leaves <c>__result</c> false); <c>true</c>
    /// to allow trade despite the war, per the configured <see cref="WarTradePolicy"/>. Each faction's
    /// alignment resolves by kingdom StringId, falling back to its culture StringId when the kingdom
    /// isn't classified (player-founded / dynamically created kingdoms resolve Neutral by kingdom id
    /// but are sided by culture) — mirroring WarOfTheRingMomentum's enrollment resolution.
    /// </summary>
    bool AllowWartimeTrade(string caravanKingdomId, string caravanCultureId, string targetKingdomId, string targetCultureId, bool isPlayerCaravan);

    /// <summary>
    /// Raise vanilla's per-caravan <c>budgetFactor</c> to at least the configured floor so even a
    /// poor caravan clears the per-category buy-value gate on more than one category (the direct
    /// fix for "caravans only buy one item"). Returns the vanilla value unchanged when disabled or
    /// non-finite.
    /// </summary>
    float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan);

    /// <summary>
    /// Resolve the caravan's starting trade gold. A higher floor saturates vanilla's
    /// <c>budgetFactor = 0.1 + clamp(gold/5000)</c>, letting more categories clear the buy gate.
    /// Never lowers the vanilla value (preserves the large/main-hero bonuses). Vanilla when disabled.
    /// </summary>
    int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan);

    /// <summary>
    /// Resolve the per-item-category gold cap. Defaults to vanilla; exposed for tuning. Vanilla when disabled.
    /// </summary>
    int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan);
}
