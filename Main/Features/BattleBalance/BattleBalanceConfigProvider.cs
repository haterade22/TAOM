using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

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
}
