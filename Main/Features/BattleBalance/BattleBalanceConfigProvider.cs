using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.BattleBalance;

public class BattleBalanceConfigProvider : IBattleBalanceConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private BattleBalanceConfig _cache;

    public BattleBalanceConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public BattleBalanceConfig GetConfig()
    {
        if (_cache != null)
            return _cache;

        var path = Path.Combine(_pathService.ModuleDataPath, "configs", "battle_balance_config.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"BattleBalanceConfigProvider: battle_balance_config.json not found at {path}, using defaults");
            _cache = new BattleBalanceConfig();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(path);
            _cache = JsonConvert.DeserializeObject<BattleBalanceConfig>(json) ?? new BattleBalanceConfig();
            // Phase 9b #140 P2 — semantic validation per csharp-architecture.md "Config Providers
            // MUST Validate". NaN TierPower["T7"] propagates through CalculateTierPower switch into
            // DefaultMilitaryPowerModel. Shipped 3× per feedback_editor_fields_are_config.md.
            ValidateConfig(_cache);
            _logger.LogInfo("BattleBalanceConfigProvider: Loaded battle_balance_config.json");
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError($"BattleBalanceConfigProvider: Failed to parse battle_balance_config.json: {ex.Message}");
            _cache = new BattleBalanceConfig();
            return _cache;
        }
    }

    private void ValidateConfig(BattleBalanceConfig config)
    {
        if (config.TroopPower?.TierPower != null)
        {
            // TierPower values: must be finite, must be > 0 (zero or negative power is nonsense).
            var defaults = new BattleBalanceConfig().TroopPower.TierPower;
            foreach (var key in config.TroopPower.TierPower.Keys.ToList())
            {
                var value = config.TroopPower.TierPower[key];
                if (!FiniteFloatValidator.IsFiniteInRange(value, 0.01f, 1000f))
                {
                    var dflt = defaults.TryGetValue(key, out var d) ? d : 1.0f;
                    _logger.LogWarning($"BattleBalanceConfig: TierPower[{key}] ({value}) invalid; reverting to {dflt}");
                    config.TroopPower.TierPower[key] = dflt;
                }
            }
        }

        if (config.CasualtyRatios?.CulturalSurvivalBonuses != null)
        {
            // CulturalSurvivalBonuses: finite, [-1, +1] (formula is `vanilla * (1 - bonus)`;
            // outside this range produces nonsense like negative survival probability).
            foreach (var key in config.CasualtyRatios.CulturalSurvivalBonuses.Keys.ToList())
            {
                var value = config.CasualtyRatios.CulturalSurvivalBonuses[key];
                if (!FiniteFloatValidator.IsFiniteInRange(value, -1f, 1f))
                {
                    _logger.LogWarning($"BattleBalanceConfig: CulturalSurvivalBonuses[{key}] ({value}) out of [-1,1]; reverting to 0");
                    config.CasualtyRatios.CulturalSurvivalBonuses[key] = 0f;
                }
            }
        }
    }
}
