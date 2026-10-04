namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Merges MCM over the validated JSON config. MCM-exposed fields read <c>X</c> through the cached
/// <c>TaomSettings</c> reference (see <c>Settings</c>) and fall back to the JSON config (which is the default source + holds the advanced, JSON-only
/// knobs). MCM slider bounds mirror the JSON validation bounds, so an MCM value can't escape the
/// validated range (the "both surfaces" invariant). The war policy resolves from the MCM dropdown
/// index, falling back to the validated JSON string.
/// </summary>
public class CaravanTradeSettingsProvider : ICaravanTradeSettingsProvider
{
    private readonly ICaravanTradeConfigProvider _configProvider;

    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    internal CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;

    private CaravanTradeConfig Cfg => _configProvider.GetConfig();

    public bool Enabled => Settings?.EnableCaravanTrade ?? Cfg.Enabled;
    public bool ApplyToPlayerCaravans => Settings?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
    public float RangeMultiplier => Settings?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;

    // JSON-only advanced curve knobs.
    public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
    public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
    public float MaxCompensation => Cfg.MaxCompensation;
    public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
    public bool HomeDistanceReweight => Cfg.HomeDistanceReweight;

    public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
    public float BudgetFactorFloor => Settings?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;

    // JSON-only.
    public int InitialTradeGold => Cfg.InitialTradeGold;
    public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;

    private WarTradePolicy ResolveWarPolicy()
    {
        var dropdown = Settings?.CaravanWarTradePolicy;
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
