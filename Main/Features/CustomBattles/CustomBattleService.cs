using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Config;

namespace TAOM.Features.CustomBattles;

public class CustomBattleService : ICustomBattleService
{


    private readonly IObjectManagerAdapter _objectManager;
    private readonly IModLogger _logger;
    private readonly ICustomBattleCommandersProvider _commandersProvider;

    private Dictionary<string, CultureInfo> _cultureCache;
    private List<CharacterInfo> _characterCache;

    public CustomBattleService(
        IObjectManagerAdapter objectManager,
        IModLogger logger,
        ICustomBattleCommandersProvider commandersProvider)
    {
        _objectManager = objectManager;
        _logger = logger;
        _commandersProvider = commandersProvider;
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
        return GetCommanderIdsForFaction(factionId, int.MaxValue);
    }

    public IReadOnlyList<string> GetCommanderIdsForFaction(string factionId, int takeMax)
    {
        if (string.IsNullOrEmpty(factionId) || takeMax <= 0)
            return new List<string>();

        // Curated override: a configured faction shows EXACTLY its ordered list — bypassing the
        // IsValidCommander regex (so 3-segment ids appear), the culture filter (so a cross-culture
        // lord may be listed), and takeMax (curated lists can exceed the cap). Curated ids are
        // filtered to those that actually exist as characters; if NONE survive (every id is a
        // typo / removed lord), we fall through to the default per-culture path rather than leaving
        // the dropdown on the unfiltered global list (Codex review 2026-06-27 finding #1).
        if (_commandersProvider.HasCuratedEntry(factionId))
        {
            var curated = _commandersProvider.GetCuratedCommanderIds(factionId)
                .Where(CharacterExists)
                .ToList();
            if (curated.Count > 0)
                return curated;

            _logger.LogWarning($"CustomBattleService: curated faction '{factionId}' resolved to no existing commanders — falling back to default selection");
            // fall through to the default per-culture path below
        }

        try
        {
            return GetCharacterCache()
                .Where(c => IsValidCommander(c) &&
                            string.Equals(c.CultureId, factionId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Take(takeMax)
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

    // Existence check by id only (culture/regex agnostic) — used to drop curated commander ids that
    // don't resolve to a real character, without re-applying the IsValidCommander regex or culture
    // filter the curated path is meant to bypass.
    private HashSet<string> _characterIdCache;

    private bool CharacterExists(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (_characterIdCache == null)
            _characterIdCache = new HashSet<string>(
                GetCharacterCache().Where(c => !string.IsNullOrEmpty(c.Id)).Select(c => c.Id),
                StringComparer.OrdinalIgnoreCase);

        return _characterIdCache.Contains(id);
    }

    private static readonly Regex _kingdomLordId =
        new Regex(@"^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsValidCommander(CharacterInfo c)
    {
        if (!c.IsHero || string.IsNullOrEmpty(c.Id))
            return false;

        return _kingdomLordId.IsMatch(c.Id);
    }
}
