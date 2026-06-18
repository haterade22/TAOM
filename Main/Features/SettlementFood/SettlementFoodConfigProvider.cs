using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.SettlementFood;

public class SettlementFoodConfigProvider : ISettlementFoodConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<SettlementFoodConfig> _config;

    public SettlementFoodConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<SettlementFoodConfig>(LoadConfig);
    }

    public SettlementFoodConfig GetConfig() => _config.Value;

    private SettlementFoodConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "settlement_food", "settlement_food_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: settlement_food_config.json not found at {path}, using defaults");
            return new SettlementFoodConfig();
        }

        SettlementFoodConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<SettlementFoodConfig>(json) ?? new SettlementFoodConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"SettlementFoodConfigProvider: Failed to parse settlement_food_config.json: {ex.Message}");
            return new SettlementFoodConfig();
        }

        return Validate(parsed);
    }

    private SettlementFoodConfig Validate(SettlementFoodConfig parsed)
    {
        var sanitized = new SettlementFoodConfig
        {
            GarrisonFoodDivisor = parsed.GarrisonFoodDivisor,
            ProsperityFoodDivisor = parsed.ProsperityFoodDivisor,
            TownBaseFood = parsed.TownBaseFood,
            CastleBaseFood = parsed.CastleBaseFood,
            VillageFoodMultiplier = parsed.VillageFoodMultiplier,
            FlatFoodBonus = parsed.FlatFoodBonus,
            FoodStocksUpperLimit = parsed.FoodStocksUpperLimit,
            CastleFoodStockUpperLimitBonus = parsed.CastleFoodStockUpperLimitBonus,
        };

        var defaults = new SettlementFoodConfig();
        var rejected = false;

        // Divisors MUST be >= 1 — a 0 divisor poisons the vanilla food formula with Infinity.
        if (sanitized.GarrisonFoodDivisor < 1 || sanitized.GarrisonFoodDivisor > 10000)
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: garrisonFoodDivisor={sanitized.GarrisonFoodDivisor} outside [1,10000], reverting to default {defaults.GarrisonFoodDivisor}");
            sanitized.GarrisonFoodDivisor = defaults.GarrisonFoodDivisor;
            rejected = true;
        }

        if (sanitized.ProsperityFoodDivisor < 1 || sanitized.ProsperityFoodDivisor > 10000)
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: prosperityFoodDivisor={sanitized.ProsperityFoodDivisor} outside [1,10000], reverting to default {defaults.ProsperityFoodDivisor}");
            sanitized.ProsperityFoodDivisor = defaults.ProsperityFoodDivisor;
            rejected = true;
        }

        // Production knobs: finite, non-negative (a negative would worsen the deficit it exists to relieve).
        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownBaseFood, 0f, 10000f))
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: townBaseFood={sanitized.TownBaseFood} must be a finite value in [0,10000], reverting to default {defaults.TownBaseFood}");
            sanitized.TownBaseFood = defaults.TownBaseFood;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.CastleBaseFood, 0f, 10000f))
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: castleBaseFood={sanitized.CastleBaseFood} must be a finite value in [0,10000], reverting to default {defaults.CastleBaseFood}");
            sanitized.CastleBaseFood = defaults.CastleBaseFood;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.VillageFoodMultiplier, 0f, 10000f))
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: villageFoodMultiplier={sanitized.VillageFoodMultiplier} must be a finite value in [0,10000], reverting to default {defaults.VillageFoodMultiplier}");
            sanitized.VillageFoodMultiplier = defaults.VillageFoodMultiplier;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.FlatFoodBonus, 0f, 100000f))
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: flatFoodBonus={sanitized.FlatFoodBonus} must be a finite value in [0,100000], reverting to default {defaults.FlatFoodBonus}");
            sanitized.FlatFoodBonus = defaults.FlatFoodBonus;
            rejected = true;
        }

        // Storage caps: town limit >= 1, castle bonus >= 0.
        if (sanitized.FoodStocksUpperLimit < 1 || sanitized.FoodStocksUpperLimit > 1000000)
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: foodStocksUpperLimit={sanitized.FoodStocksUpperLimit} outside [1,1000000], reverting to default {defaults.FoodStocksUpperLimit}");
            sanitized.FoodStocksUpperLimit = defaults.FoodStocksUpperLimit;
            rejected = true;
        }

        if (sanitized.CastleFoodStockUpperLimitBonus < 0 || sanitized.CastleFoodStockUpperLimitBonus > 1000000)
        {
            _logger.LogWarning($"SettlementFoodConfigProvider: castleFoodStockUpperLimitBonus={sanitized.CastleFoodStockUpperLimitBonus} outside [0,1000000], reverting to default {defaults.CastleFoodStockUpperLimitBonus}");
            sanitized.CastleFoodStockUpperLimitBonus = defaults.CastleFoodStockUpperLimitBonus;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("SettlementFoodConfigProvider: settlement_food_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("SettlementFoodConfigProvider: Loaded settlement_food_config.json");

        return sanitized;
    }
}
