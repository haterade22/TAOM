using System;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.SpecialResources;
using TAOM.Features.StartupResources;

namespace TAOM.Features.PlayerPossession;

/// <inheritdoc />
public sealed class JoinReconciliationService : IJoinReconciliationService
{
    private readonly IHeroRosterAdapter _heroRoster;
    private readonly IRaceManager _raceManager;
    private readonly IPlayerStartupGoldService _startupGold;
    private readonly ICareerCreationHandler _careerHandler;
    private readonly ISpecialResourceService _specialResources;
    private readonly IModLogger _logger;

    public JoinReconciliationService(
        IHeroRosterAdapter heroRoster,
        IRaceManager raceManager,
        IPlayerStartupGoldService startupGold,
        ICareerCreationHandler careerHandler,
        ISpecialResourceService specialResources,
        IModLogger logger)
    {
        _heroRoster = heroRoster;
        _raceManager = raceManager;
        _startupGold = startupGold;
        _careerHandler = careerHandler;
        _specialResources = specialResources;
        _logger = logger;
    }

    public bool ReapplyCharacterCreationPackage(
        PlayerCharacterCreationChoices choices, string heroId, string kingdomId)
    {
        if (choices == null || string.IsNullOrEmpty(heroId)) return false;

        // The CULTURE the player picked drives the grants, not whatever culture the host's hero
        // carries. The player chose Mirkwood and earned Mirkwood's package; arriving as the host's
        // culture is the bug being fixed, not a new source of truth.
        var applied = false;
        applied |= TryApply("race", () => ApplyRace(choices.RaceId, heroId));
        applied |= TryApply("startup gold", () => ApplyStartupGold(choices.CultureId, heroId));
        applied |= TryApply("career", () => ApplyCareer(choices.CareerId, heroId));
        applied |= TryApply("special-resource seed", () => ApplySpecialResourceSeed(heroId, kingdomId, choices.CultureId));

        if (applied)
            _logger.LogInfo($"[Possession] Re-applied the character-creation package to '{heroId}'.");

        return applied;
    }

    // Independently guarded so a single failing grant cannot cost the player the rest — a joiner
    // losing their career because the gold grant threw would be a worse bug than the one this fixes.
    private bool TryApply(string what, Func<bool> grant)
    {
        try
        {
            return grant();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Possession] Re-applying {what} failed: {ex.Message}");
            return false;
        }
    }

    private bool ApplyRace(int raceId, string heroId)
    {
        // Validate BEFORE the set: GetRaceNameFromId coerces unknown ids to "human", so an id from a
        // module set this client does not have would be written as a valid-looking race and then
        // cached for the session (csharp-architecture.md, validate-before-lookup).
        if (raceId < 0) return false;
        if (raceId != 0 && !_raceManager.IsValidRaceId(raceId)) return false;

        if (_heroRoster.GetHeroRace(heroId) == raceId) return false;

        _heroRoster.SetHeroRace(heroId, raceId);
        _logger.LogInfo($"[Possession] Restored character-creation race {raceId} on '{heroId}'.");
        return true;
    }

    private bool ApplyStartupGold(string cultureId, string heroId)
    {
        if (string.IsNullOrEmpty(cultureId)) return false;

        _startupGold.GrantPlayerStartupGold(cultureId, heroId);
        return true;
    }

    private bool ApplyCareer(string careerId, string heroId)
    {
        if (string.IsNullOrEmpty(careerId) || _careerHandler == null) return false;

        _careerHandler.OnCareerSelected(heroId, careerId);
        _logger.LogInfo($"[Possession] Restored character-creation career '{careerId}' on '{heroId}'.");
        return true;
    }

    private bool ApplySpecialResourceSeed(string heroId, string kingdomId, string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return false;
        if (_specialResources.ResolveResource(kingdomId, cultureId) == null) return false;

        _specialResources.InitializeHero(heroId, kingdomId, cultureId);
        return true;
    }
}
