using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CustomBattles;

public class CustomBattleService : ICustomBattleService
{
    private static readonly string[] ExcludedPrefixes = { "companion", "child", "tutorial", "commander_", "wanderer", "notable" };

    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;

    private Dictionary<string, CultureInfo> _cultureCache;
    private List<CharacterInfo> _characterCache;

    public CustomBattleService(IObjectManagerAdapter objectManager, IModLogger logger)
    {
        _objectManager = objectManager;
        _logger = logger;
    }

    public IReadOnlyList<string> GetFactionIds()
    {
        try
        {
            return GetCultureCache().Values
                .Where(c => c.CanHaveSettlement && !c.IsBandit)
                .Select(c => c.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleService: Failed to get faction IDs: {ex.Message}");
            return new List<string>();
        }
    }

    public IReadOnlyList<string> GetCommanderIds()
    {
        try
        {
            return GetCharacterCache()
                .Where(IsValidCommander)
                .Select(c => c.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleService: Failed to get commander IDs: {ex.Message}");
            return new List<string>();
        }
    }

    public IReadOnlyList<string> GetCommanderIdsForFaction(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
            return new List<string>();

        try
        {
            return GetCharacterCache()
                .Where(c => IsValidCommander(c) &&
                            string.Equals(c.CultureId, factionId, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Id)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleService: Failed to get commanders for faction '{factionId}': {ex.Message}");
            return new List<string>();
        }
    }

    public string GetDefaultTroopIdForFormation(string factionId, int formationIndex)
    {
        if (string.IsNullOrEmpty(factionId))
            return null;

        try
        {
            var cache = GetCultureCache();
            if (!cache.TryGetValue(factionId.ToLowerInvariant(), out var culture))
                return null;

            return formationIndex switch
            {
                0 => culture.MeleeMilitiaTroopId ?? culture.BasicTroopId,
                1 => culture.RangedMilitiaTroopId,
                2 => culture.EliteBasicTroopId,
                3 => culture.RangedEliteMilitiaTroopId,
                _ => culture.BasicTroopId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleService: Failed to get troop for formation {formationIndex}: {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, CultureInfo> GetCultureCache()
    {
        if (_cultureCache != null)
            return _cultureCache;

        _cultureCache = _objectManager.GetAllCultureInfos()
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .ToDictionary(c => c.Id.ToLowerInvariant(), c => c);

        return _cultureCache;
    }

    private List<CharacterInfo> GetCharacterCache()
    {
        if (_characterCache != null)
            return _characterCache;

        _characterCache = _objectManager.GetAllCharacterInfos().ToList();
        return _characterCache;
    }

    private static bool IsValidCommander(CharacterInfo c)
    {
        if (!c.IsHero || string.IsNullOrEmpty(c.Id))
            return false;

        var idLower = c.Id.ToLowerInvariant();
        foreach (var prefix in ExcludedPrefixes)
        {
            if (idLower.Contains(prefix))
                return false;
        }

        return true;
    }
}
