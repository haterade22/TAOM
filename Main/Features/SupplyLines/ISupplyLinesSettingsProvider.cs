namespace TAOM.Features.SupplyLines;

/// <summary>
/// MCM boundary. Trivial bridge over <c>TaomSettings</c>; validation (finite, range) happens here
/// so a stale json2 value cannot reach pricing or movement (Config Providers MUST Validate).
/// </summary>
public interface ISupplyLinesSettingsProvider
{
    /// <summary>Master toggle. Off = no menu options, no ticks; existing orders keep advancing to
    /// completion so cargo is not stranded by a mid-transit toggle.</summary>
    bool Enabled { get; }

    /// <summary>Price multiplier on ordered goods (default 1.05).</summary>
    float GoodsMarkupFactor { get; }

    /// <summary>Delivery fee per map-distance unit (default 2).</summary>
    float TransportFeePerDistance { get; }

    /// <summary>Mercenary escort wage per map-distance unit (default 10).</summary>
    float MercenaryWagePerDistance { get; }

    /// <summary>Mercenary guards added to an escorted caravan (default 10).</summary>
    int MercenaryGuardCount { get; }

    /// <summary>Caravan travel hours per map-distance unit (default 2). The source module reused a
    /// dead handcart constant for this; named honestly here.</summary>
    float CaravanHoursPerDistance { get; }

    /// <summary>Draw the on-map route arrows for in-transit orders (default true).</summary>
    bool ShowRouteVisual { get; }
}
