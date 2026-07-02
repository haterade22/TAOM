using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.SettlementEconomy;

public class SettlementEconomyConfigProvider : ISettlementEconomyConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<SettlementEconomyConfig> _config;

    public SettlementEconomyConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<SettlementEconomyConfig>(LoadConfig);
    }

    public SettlementEconomyConfig GetConfig() => _config.Value;

    private SettlementEconomyConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "settlement_economy", "settlement_economy_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SettlementEconomyConfigProvider: settlement_economy_config.json not found at {path}, using defaults");
            return new SettlementEconomyConfig();
        }

        SettlementEconomyConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<SettlementEconomyConfig>(json) ?? new SettlementEconomyConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"SettlementEconomyConfigProvider: Failed to parse settlement_economy_config.json: {ex.Message}");
            return new SettlementEconomyConfig();
        }

        return Validate(parsed);
    }

    private SettlementEconomyConfig Validate(SettlementEconomyConfig parsed)
    {
        var sanitized = new SettlementEconomyConfig
        {
            TownGoldBase = parsed.TownGoldBase,
            TownGoldPerProsperity = parsed.TownGoldPerProsperity,
            TownGoldRegenRate = parsed.TownGoldRegenRate,
        };

        var defaults = new SettlementEconomyConfig();
        var rejected = false;

        // A negative base could make regen permanently negative at low prosperity — reintroducing
        // the exact "towns never recover" bug this feature exists to fix (#317).
        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldBase, 0f, 200000f))
        {
            _logger.LogWarning($"SettlementEconomyConfigProvider: townGoldBase={sanitized.TownGoldBase} must be a finite value in [0,200000], reverting to default {defaults.TownGoldBase}");
            sanitized.TownGoldBase = defaults.TownGoldBase;
            rejected = true;
        }

        // A negative slope inverts prosperity's meaning (sign-flip rule).
        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldPerProsperity, 0f, 100f))
        {
            _logger.LogWarning($"SettlementEconomyConfigProvider: townGoldPerProsperity={sanitized.TownGoldPerProsperity} must be a finite value in [0,100], reverting to default {defaults.TownGoldPerProsperity}");
            sanitized.TownGoldPerProsperity = defaults.TownGoldPerProsperity;
            rejected = true;
        }

        // Negative rate inverts mean-reversion (the model would drain towns); rate > 1 overshoots
        // the target and oscillates. 0 is allowed as the explicit "freeze regen" extreme.
        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldRegenRate, 0f, 1f))
        {
            _logger.LogWarning($"SettlementEconomyConfigProvider: townGoldRegenRate={sanitized.TownGoldRegenRate} must be a finite value in [0,1], reverting to default {defaults.TownGoldRegenRate}");
            sanitized.TownGoldRegenRate = defaults.TownGoldRegenRate;
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("SettlementEconomyConfigProvider: settlement_economy_config.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("SettlementEconomyConfigProvider: Loaded settlement_economy_config.json");

        return sanitized;
    }
}
