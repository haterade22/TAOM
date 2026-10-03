using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Config;
using TaleWorlds.Core;

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
            // HasFactionBanner: vanilla CustomBattleHelper.GetCustomBattleParties recolours layer 0 of the
            // faction banner unguarded, so a culture without faction_banner_key (vanilla nord, vakken,
            // darshi) throws ArgumentOutOfRangeException on Start (crash f9a7181d).
            return GetCultureCache().Values
                .Where(c => c.CanHaveSettlement && !c.IsBandit && c.HasFactionBanner)
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

    public string GetDefaultTroopIdForFormation(string factionId, int formationIndex, bool vanillaHasPick)
    {
        if (string.IsNullOrEmpty(factionId))
            return null;

        try
        {
            var cache = GetCultureCache();
            if (!cache.TryGetValue(factionId.ToLowerInvariant(), out var culture))
                return null;

            var candidates = formationIndex switch
            {
                0 => new[] { culture.MeleeMilitiaTroopId, culture.BasicTroopId },
                1 => new[] { culture.RangedMilitiaTroopId },
                2 => new[] { culture.EliteBasicTroopId },
                3 => new[] { culture.RangedEliteMilitiaTroopId },
                _ => Array.Empty<string>()
            };

            // With no vanilla pick, a non-fitting troop is still better than null: the slot list ignores it either way,
            // but CustomBattleHelper.PopulateListsWithDefaults spawns the default unchecked when the slot is empty
            // (a culture with no soldiers of its own, Abanissa and Shaghana), and a null default for slots 0-2 throws
            // at Start once that slot has troops to spawn (a null horse-archer default is redistributed instead).
            return candidates.FirstOrDefault(id => IsEligibleForSlot(id, culture.Id, formationIndex))
                ?? (vanillaHasPick ? null : candidates.FirstOrDefault(CharacterExists));
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleService: Failed to get troop for formation {formationIndex}: {ex.Message}");
            return null;
        }
    }

    // Vanilla's Custom Battle slot list (v1.5.3) holds a troop only when it is a soldier and not obsolete
    // (ArmyCompositionGroupVM, IsSoldier && !IsObsolete) and is the slot's culture with a fitting
    // DefaultFormationClass (ArmyCompositionItemVM.IsValidUnitItem). There a default outside that list is ignored
    // and the slot's first troop becomes the default. Culture data often names a troop that cannot fit (a foot archer
    // for the horse-archer slot, another culture's line), and returning it would replace vanilla's pick for nothing.
    private static readonly FormationClass[][] SlotFormationClasses =
    {
        new[] { FormationClass.Infantry, FormationClass.HeavyInfantry },
        new[] { FormationClass.Ranged },
        new[] { FormationClass.Cavalry, FormationClass.LightCavalry, FormationClass.HeavyCavalry },
        new[] { FormationClass.HorseArcher }
    };

    private bool IsEligibleForSlot(string troopId, string cultureId, int formationIndex)
    {
        var troop = FindCharacter(troopId);
        if (troop == null || !troop.IsSoldier || troop.IsObsolete
            || !string.Equals(troop.CultureId, cultureId, StringComparison.OrdinalIgnoreCase))
            return false;

        return SlotFormationClasses[formationIndex].Contains(troop.DefaultFormationClass);
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

    // Lookup by id only (culture/regex agnostic). CharacterExists drops curated commander ids that don't
    // resolve to a real character, without re-applying the IsValidCommander regex or culture filter the
    // curated path is meant to bypass; IsEligibleForSlot reads the troop's slot fields.
    private Dictionary<string, CharacterInfo> _characterById;

    private CharacterInfo FindCharacter(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (_characterById == null)
        {
            _characterById = new Dictionary<string, CharacterInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in GetCharacterCache())
                if (!string.IsNullOrEmpty(c.Id) && !_characterById.ContainsKey(c.Id))
                    _characterById.Add(c.Id, c); // first wins
        }

        return _characterById.TryGetValue(id, out var character) ? character : null;
    }

    private bool CharacterExists(string id) => FindCharacter(id) != null;

    private static readonly Regex _kingdomLordId =
        new Regex(@"^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsValidCommander(CharacterInfo c)
    {
        if (!c.IsHero || string.IsNullOrEmpty(c.Id))
            return false;

        return _kingdomLordId.IsMatch(c.Id);
    }
}
