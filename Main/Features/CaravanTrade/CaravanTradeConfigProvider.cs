using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Every numeric field is
/// range-checked (NaN/Infinity rejected via <see cref="FiniteFloatValidator"/>); the war-policy
/// string is validated against the known set (the M1 typo trap); invalid values revert to the
/// shipped default with a warning, and the master toggle stays effective. Cached (Reuse.Singleton) —
/// config edits require an app restart.
/// </summary>
public class CaravanTradeConfigProvider : ICaravanTradeConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<CaravanTradeConfig> _config;

    public CaravanTradeConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<CaravanTradeConfig>(LoadConfig);
    }

    public CaravanTradeConfig GetConfig() => _config.Value;

    private CaravanTradeConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "caravan_trade", "caravan_trade_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: caravan_trade_config.json not found at {path}, using defaults");
            return new CaravanTradeConfig();
        }

        CaravanTradeConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CaravanTradeConfig>(json) ?? new CaravanTradeConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CaravanTradeConfigProvider: Failed to parse caravan_trade_config.json: {ex.Message}");
            return new CaravanTradeConfig();
        }

        return Validate(parsed);
    }

    private CaravanTradeConfig Validate(CaravanTradeConfig parsed)
    {
        var defaults = new CaravanTradeConfig();
        var c = new CaravanTradeConfig
        {
            Enabled = parsed.Enabled,
            ApplyToPlayerCaravans = parsed.ApplyToPlayerCaravans,
            RangeMultiplier = parsed.RangeMultiplier,
            DistanceDecayExponent = parsed.DistanceDecayExponent,
            NearFieldFlattenDays = parsed.NearFieldFlattenDays,
            MaxCompensation = parsed.MaxCompensation,
            AntiShuttlePenalty = parsed.AntiShuttlePenalty,
            HomeDistanceReweight = parsed.HomeDistanceReweight,
            WarTradePolicy = parsed.WarTradePolicy,
            BudgetFactorFloor = parsed.BudgetFactorFloor,
            InitialTradeGold = parsed.InitialTradeGold,
            MaxGoldPerCategory = parsed.MaxGoldPerCategory,
        };

        var rejected = false;

        // Below 1 would shrink the range below vanilla (worse shuttle); above 4 lets caravans wander map-wide.
        if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: rangeMultiplier={c.RangeMultiplier} must be finite in [1,4], reverting to {defaults.RangeMultiplier}");
            c.RangeMultiplier = defaults.RangeMultiplier;
            rejected = true;
        }

        // Shape only: 0/negative degenerates the curve; > 4 is a near-vanilla steep decay.
        if (!FiniteFloatValidator.IsFiniteInRange(c.DistanceDecayExponent, 0.25f, 4f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: distanceDecayExponent={c.DistanceDecayExponent} must be finite in [0.25,4], reverting to {defaults.DistanceDecayExponent}");
            c.DistanceDecayExponent = defaults.DistanceDecayExponent;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(c.NearFieldFlattenDays, 0f, 20f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: nearFieldFlattenDays={c.NearFieldFlattenDays} must be finite in [0,20], reverting to {defaults.NearFieldFlattenDays}");
            c.NearFieldFlattenDays = defaults.NearFieldFlattenDays;
            rejected = true;
        }

        // Must be ≥ 1 (a cap below 1 would suppress every score); ≤ 20 keeps far towns from dominating.
        if (!FiniteFloatValidator.IsFiniteInRange(c.MaxCompensation, 1f, 20f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: maxCompensation={c.MaxCompensation} must be finite in [1,20], reverting to {defaults.MaxCompensation}");
            c.MaxCompensation = defaults.MaxCompensation;
            rejected = true;
        }

        // A fraction. > 1 would flip the score sign; < 0 would reward returning to the just-left town.
        if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
            c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
            rejected = true;
        }

        // M1 string-branch trap: an unknown/typo policy must revert, not silently take the switch default.
        if (!WarTradePolicyParser.TryParse(c.WarTradePolicy, out _))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: warTradePolicy='{c.WarTradePolicy}' is not one of None/IgnoreWar/SameAlignmentAndNeutral, reverting to {defaults.WarTradePolicy}");
            c.WarTradePolicy = defaults.WarTradePolicy;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(c.BudgetFactorFloor, 0f, 1f))
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: budgetFactorFloor={c.BudgetFactorFloor} must be finite in [0,1], reverting to {defaults.BudgetFactorFloor}");
            c.BudgetFactorFloor = defaults.BudgetFactorFloor;
            rejected = true;
        }

        if (c.InitialTradeGold < 1000 || c.InitialTradeGold > 100000)
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: initialTradeGold={c.InitialTradeGold} must be in [1000,100000], reverting to {defaults.InitialTradeGold}");
            c.InitialTradeGold = defaults.InitialTradeGold;
            rejected = true;
        }

        if (c.MaxGoldPerCategory < 100 || c.MaxGoldPerCategory > 20000)
        {
            _logger.LogWarning($"CaravanTradeConfigProvider: maxGoldPerCategory={c.MaxGoldPerCategory} must be in [100,20000], reverting to {defaults.MaxGoldPerCategory}");
            c.MaxGoldPerCategory = defaults.MaxGoldPerCategory;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("CaravanTradeConfigProvider: caravan_trade_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("CaravanTradeConfigProvider: Loaded caravan_trade_config.json");

        return c;
    }
}
