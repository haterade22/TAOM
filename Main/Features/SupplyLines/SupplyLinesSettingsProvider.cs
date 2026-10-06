using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Production wire-up of <see cref="ISupplyLinesSettingsProvider"/> over the MCM
/// <c>TaomSettings</c> singleton. Values are re-validated here because a stale json2 file can hold
/// anything: non-finite or out-of-range falls back to the shipped default rather than reaching
/// pricing or movement (Config Providers MUST Validate; both-surfaces rule).
/// </summary>
public class SupplyLinesSettingsProvider : ISupplyLinesSettingsProvider
{
    // Enabled and ShowRouteVisual are read every campaign frame, so the settings object is taken once, on the first non-null read, and read through
    // (BattleBalanceSettingsProvider pattern; CampaignHotPathSettingsProvidersTests, #746).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SupplyLinesSettingsProvider() { }
    internal SupplyLinesSettingsProvider(TaomSettings settings) => _settings = settings;

    public const float DefaultGoodsMarkup = 1.05f;
    public const float DefaultTransportFee = 2f;
    public const float DefaultMercWage = 10f;
    public const int DefaultGuardCount = 10;
    public const float DefaultHoursPerDistance = 2f;

    public bool Enabled => Settings?.EnableSupplyLines ?? true;

    public float GoodsMarkupFactor =>
        Sane(Settings?.SupplyGoodsMarkupFactor, 1.0f, 3.0f, DefaultGoodsMarkup);

    public float TransportFeePerDistance =>
        Sane(Settings?.SupplyTransportFeePerDistance, 0f, 20f, DefaultTransportFee);

    public float MercenaryWagePerDistance =>
        Sane(Settings?.SupplyMercenaryWagePerDistance, 0f, 50f, DefaultMercWage);

    public int MercenaryGuardCount
    {
        get
        {
            var raw = Settings?.SupplyMercenaryGuardCount ?? DefaultGuardCount;
            return raw < 0 || raw > 40 ? DefaultGuardCount : raw;
        }
    }

    public float CaravanHoursPerDistance =>
        Sane(Settings?.SupplyCaravanHoursPerDistance, 0.5f, 10f, DefaultHoursPerDistance);

    public bool ShowRouteVisual => Settings?.SupplyShowRouteVisual ?? true;

    private static float Sane(float? raw, float min, float max, float fallback)
    {
        if (!raw.HasValue)
            return fallback;
        return FiniteFloatValidator.IsFiniteInRange(raw.Value, min, max) ? raw.Value : fallback;
    }
}
