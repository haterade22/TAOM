namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Single merged read surface for the CaravanTrade feature: MCM-over-JSON. For fields exposed in
/// MCM the implementation reads <c>TaomSettings.Instance?.X</c> and falls back to the validated
/// JSON config; JSON-only advanced fields come straight from the config. The service depends only
/// on this interface (plus <see cref="Execution.IAlignmentService"/> + logger), so all validation
/// and the MCM/JSON merge live in one place.
/// </summary>
public interface ICaravanTradeSettingsProvider
{
    /// <summary>Master toggle. Off ⇒ every service method returns the vanilla value ⇒ exact vanilla behavior.</summary>
    bool Enabled { get; }

    /// <summary>Whether the changes apply to player-owned caravans too (else only NPC caravans change).</summary>
    bool ApplyToPlayerCaravans { get; }

    /// <summary>Multiplier on the vanilla "very far" distance ceiling — how much further caravans may range.</summary>
    float RangeMultiplier { get; }

    /// <summary>Distance-decay exponent (alpha) in <c>1/(nearFieldFlatten+days)^alpha</c>. Lower ⇒ ranges further.</summary>
    float DistanceDecayExponent { get; }

    /// <summary>Days added inside the decay denominator so near towns tie on distance and profit decides.</summary>
    float NearFieldFlattenDays { get; }

    /// <summary>Upper clamp on the score multiplier so one hyper-profitable far town can't pull caravans map-wide.</summary>
    float MaxCompensation { get; }

    /// <summary>Recency penalty strength: max fractional cut on the most-recently-visited town, decaying over the last few towns visited (0 = off, 1 = fully deprioritize).</summary>
    float AntiShuttlePenalty { get; }

    /// <summary>Whether to distance-compress the home town like any other (true, default = fixes the home rubber-band) or keep the old home distance exemption (false).</summary>
    bool HomeDistanceReweight { get; }

    /// <summary>Resolved war-trade policy (MCM dropdown over validated JSON string).</summary>
    WarTradePolicy WarTradePolicy { get; }

    /// <summary>Floor applied to vanilla's per-caravan budget factor so more categories clear the buy gate.</summary>
    float BudgetFactorFloor { get; }

    /// <summary>Starting trade-gold floor (vanilla 10000) — higher saturates budgetFactor for fuller baskets.</summary>
    int InitialTradeGold { get; }

    /// <summary>Per-item-category gold cap (vanilla 1500).</summary>
    int MaxGoldPerCategory { get; }
}
