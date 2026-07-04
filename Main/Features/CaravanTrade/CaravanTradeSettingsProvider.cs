namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Merges MCM over the validated JSON config. MCM-exposed fields read <c>TaomSettings.Instance?.X</c>
/// and fall back to the JSON config (which is the default source + holds the advanced, JSON-only
/// knobs). MCM slider bounds mirror the JSON validation bounds, so an MCM value can't escape the
/// validated range (the "both surfaces" invariant). The war policy resolves from the MCM dropdown
/// index, falling back to the validated JSON string.
/// </summary>
public class CaravanTradeSettingsProvider : ICaravanTradeSettingsProvider
{
    private readonly ICaravanTradeConfigProvider _configProvider;

    public CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    private CaravanTradeConfig Cfg => _configProvider.GetConfig();

    public bool Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;
    public bool ApplyToPlayerCaravans => TaomSettings.Instance?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
    public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;

    // JSON-only advanced curve knobs.
    public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
    public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
    public float MaxCompensation => Cfg.MaxCompensation;
    public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;

    public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
    public float BudgetFactorFloor => TaomSettings.Instance?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;

    // JSON-only.
    public int InitialTradeGold => Cfg.InitialTradeGold;
    public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;

    private WarTradePolicy ResolveWarPolicy()
    {
        var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;
        if (dropdown != null)
        {
            switch (dropdown.SelectedIndex)
            {
                case 0: return WarTradePolicy.None;
                case 1: return WarTradePolicy.SameAlignmentAndNeutral;
                case 2: return WarTradePolicy.IgnoreWar;
            }
        }

        // Fall back to the validated JSON string (already normalized to the known set by the provider).
        return WarTradePolicyParser.TryParse(Cfg.WarTradePolicy, out var policy)
            ? policy
            : WarTradePolicy.SameAlignmentAndNeutral;
    }
}
