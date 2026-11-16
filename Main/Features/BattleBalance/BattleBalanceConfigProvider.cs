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
            var parsed = JsonConvert.DeserializeObject<BattleBalanceConfig>(json) ?? new BattleBalanceConfig();
            // Phase 9b #140 P2 — semantic validation per csharp-architecture.md "Config Providers
            // MUST Validate". NaN TierPower["T7"] propagates through CalculateTierPower switch into
            // DefaultMilitaryPowerModel. Shipped 3× per feedback_editor_fields_are_config.md.
            // Validated before it is cached, so no reader sees the unvalidated object.
            _cache = Validate(parsed);
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

    internal BattleBalanceConfig Validate(BattleBalanceConfig config)
    {
        var compiled = new BattleBalanceConfig();
        var reverted = 0;

        // A JSON "null" for a section or table would make every consumer's dictionary read throw.
        if (config.TroopPower == null)
        {
            _logger.LogWarning("BattleBalanceConfig: TroopPower is null; using the compiled section");
            config.TroopPower = compiled.TroopPower;
            reverted++;
        }
        else if (config.TroopPower.TierPower == null)
        {
            _logger.LogWarning("BattleBalanceConfig: TroopPower.TierPower is null; using the compiled table");
            config.TroopPower.TierPower = compiled.TroopPower.TierPower;
            reverted++;
        }

        if (config.CasualtyRatios == null)
        {
            _logger.LogWarning("BattleBalanceConfig: CasualtyRatios is null; using the compiled section");
            config.CasualtyRatios = compiled.CasualtyRatios;
            reverted++;
        }
        else if (config.CasualtyRatios.CulturalSurvivalBonuses == null)
        {
            _logger.LogWarning("BattleBalanceConfig: CasualtyRatios.CulturalSurvivalBonuses is null; using the compiled table");
            config.CasualtyRatios.CulturalSurvivalBonuses = compiled.CasualtyRatios.CulturalSurvivalBonuses;
            reverted++;
        }

        if (config.AutoResolve == null)
        {
            _logger.LogWarning("BattleBalanceConfig: AutoResolve is null; using the compiled section");
            config.AutoResolve = compiled.AutoResolve;
            reverted++;
        }
        else if (!FiniteFloatValidator.IsFiniteInRange(config.AutoResolve.PowerRatioExponent, 0f, 1f))
        {
            var dflt = compiled.AutoResolve.PowerRatioExponent;
            _logger.LogWarning($"BattleBalanceConfig: AutoResolve.PowerRatioExponent ({config.AutoResolve.PowerRatioExponent}) out of [0,1]; reverting to {dflt}");
            config.AutoResolve.PowerRatioExponent = dflt;
            reverted++;
        }

        if (config.SiegeDefence == null)
        {
            _logger.LogWarning("BattleBalanceConfig: SiegeDefence is null; using the compiled section");
            config.SiegeDefence = compiled.SiegeDefence;
            reverted++;
        }
        else if (config.SiegeDefence.DefenderAdvantageBonuses == null)
        {
            _logger.LogWarning("BattleBalanceConfig: SiegeDefence.DefenderAdvantageBonuses is null; using the compiled table");
            config.SiegeDefence.DefenderAdvantageBonuses = compiled.SiegeDefence.DefenderAdvantageBonuses;
            reverted++;
        }

        if (config.SiegeDefence.UpkeepFreeGarrisons == null)
        {
            _logger.LogWarning("BattleBalanceConfig: SiegeDefence.UpkeepFreeGarrisons is null; using the compiled table");
            config.SiegeDefence.UpkeepFreeGarrisons = compiled.SiegeDefence.UpkeepFreeGarrisons;
            reverted++;
        }

        if (config.SiegeDefence.DailyGarrisonGrowth == null)
        {
            _logger.LogWarning("BattleBalanceConfig: SiegeDefence.DailyGarrisonGrowth is null; using the compiled table");
            config.SiegeDefence.DailyGarrisonGrowth = compiled.SiegeDefence.DailyGarrisonGrowth;
            reverted++;
        }

        // A bad value reverts to its compiled row. A key with no compiled row is removed, so the lookup's own
        // fallback decides: the tier formula in GetTierPower, and 0 (the engine's own chance) for survival.
        // TierPower values: finite and > 0 (zero or negative power is nonsense).
        reverted += RevertInvalid(config.TroopPower.TierPower, compiled.TroopPower.TierPower,
            value => FiniteFloatValidator.IsFiniteInRange(value, 0.01f, 1000f), "TierPower", "invalid");

        // CulturalSurvivalBonuses: finite, [-1, +1]. The formula is `vanilla * (1 - bonus)`: above 1 the death
        // chance goes negative, below -1 it more than doubles. Reverting to the compiled row means a typo in
        // the Gundabad penalty keeps the penalty.
        reverted += RevertInvalid(config.CasualtyRatios.CulturalSurvivalBonuses, compiled.CasualtyRatios.CulturalSurvivalBonuses,
            value => FiniteFloatValidator.IsFiniteInRange(value, -1f, 1f), "CulturalSurvivalBonuses", "out of [-1,1]");

        // DefenderAdvantageBonuses: finite, [0, 1]. A negative factor would weaken the defenders; above 1 more than
        // doubles their strikes.
        reverted += RevertInvalid(config.SiegeDefence.DefenderAdvantageBonuses, compiled.SiegeDefence.DefenderAdvantageBonuses,
            value => FiniteFloatValidator.IsFiniteInRange(value, 0f, 1f), "DefenderAdvantageBonuses", "out of [0,1]");

        // DailyGarrisonGrowth: finite, [0, 20] troops a day per fortification.
        reverted += RevertInvalid(config.SiegeDefence.DailyGarrisonGrowth, compiled.SiegeDefence.DailyGarrisonGrowth,
            value => FiniteFloatValidator.IsFiniteInRange(value, 0f, 20f), "DailyGarrisonGrowth", "out of [0,20]");

        if (reverted > 0)
            _logger.LogWarning($"BattleBalanceConfig: {reverted} value(s) reverted to compiled defaults; see the warnings above");

        return config;
    }

    private int RevertInvalid(Dictionary<string, float> table, Dictionary<string, float> compiled,
        Func<float, bool> isValid, string tableName, string reason)
    {
        var reverted = 0;
        foreach (var key in table.Keys.ToList())
        {
            var value = table[key];
            if (isValid(value))
                continue;

            reverted++;
            if (compiled.TryGetValue(key, out var dflt))
            {
                _logger.LogWarning($"BattleBalanceConfig: {tableName}[{key}] ({value}) {reason}; reverting to {dflt}");
                table[key] = dflt;
            }
            else
            {
                _logger.LogWarning($"BattleBalanceConfig: {tableName}[{key}] ({value}) {reason}; removing the row, no compiled default");
                table.Remove(key);
            }
        }

        return reverted;
    }
}
