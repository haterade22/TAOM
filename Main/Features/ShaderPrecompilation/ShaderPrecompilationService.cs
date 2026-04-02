using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderPrecompilation;

public class ShaderPrecompilationService : IShaderPrecompilationService
{
    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;

    public ShaderPrecompilationService(IObjectManagerAdapter objectManager, IModLogger logger)
    {
        _objectManager = objectManager;
        _logger = logger;
    }

    public IReadOnlyList<string> GetCharacterIdsForShaderBattle()
    {
        try
        {
            var validCultureIds = GetValidCultureIds();
            return _objectManager.GetAllCharacterInfos()
                .Where(c => !string.IsNullOrEmpty(c.Id)
                         && c.CultureId != null
                         && validCultureIds.Contains(c.CultureId))
                .Select(c => c.Id)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ShaderPrecompilation] Failed to build character list: {ex.Message}");
            return new List<string>();
        }
    }

    public IReadOnlyList<string> GetCultureIdsForShaderBattle()
    {
        try
        {
            return GetValidCultureIds().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ShaderPrecompilation] Failed to get culture IDs: {ex.Message}");
            return new List<string>();
        }
    }

    private HashSet<string> _validCultureIdsCache;

    private HashSet<string> GetValidCultureIds()
    {
        if (_validCultureIdsCache != null)
            return _validCultureIdsCache;

        _validCultureIdsCache = _objectManager.GetAllCultureInfos()
            .Where(c => !string.IsNullOrEmpty(c.Id) && !c.IsBandit)
            .Select(c => c.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _validCultureIdsCache;
    }
}
