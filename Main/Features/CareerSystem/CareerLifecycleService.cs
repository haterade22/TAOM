using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerLifecycleService : ICareerLifecycleService
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerCreationHandler _creationHandler;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerPurseAdapter _purse;
    private readonly IModLogger _logger;

    public CareerLifecycleService(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerCreationHandler creationHandler,
        ICareerPassiveService passiveService,
        ICareerPurseAdapter purse,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _creationHandler = creationHandler;
        _passiveService = passiveService;
        _purse = purse;
        _logger = logger;
    }

    public bool AssignFallbackCareerIfMissing(string heroStringId, string cultureId)
    {
        if (string.IsNullOrEmpty(heroStringId)) return false;
        if (_dataService.HasCareer(heroStringId))
        {
            if (!IsUnknownCareer(_dataService.GetCareerStringId(heroStringId))) return false;

            _logger.LogWarning($"CareerSystem: hero '{heroStringId}' holds unknown career '{_dataService.GetCareerStringId(heroStringId)}', clearing it for reassignment");
            _dataService.ClearCareer(heroStringId);
        }

        _logger.LogInfo($"CareerSystem: Legacy save detected — hero '{heroStringId}' has no career (culture='{cultureId ?? "null"}')");
        if (string.IsNullOrEmpty(cultureId)) return false;

        foreach (var career in _registry.GetAllCareers())
        {
            var eligible = false;
            foreach (var id in career.EligibleCultureIds)
            {
                if (string.Equals(id, cultureId, StringComparison.OrdinalIgnoreCase))
                { eligible = true; break; }
            }
            if (eligible)
            {
                _creationHandler.OnCareerSelected(heroStringId, career.Id);
                _logger.LogInfo($"CareerSystem: Legacy fallback — assigned career '{career.Id}' to '{heroStringId}'");
                return true;
            }
        }
        return false;
    }

    private bool IsUnknownCareer(string careerId)
    {
        if (string.IsNullOrEmpty(careerId)) return false;
        var all = _registry.GetAllCareers();
        return all != null && all.Count > 0 && _registry.GetCareer(careerId) == null;
    }

    public int RepairForeignChoices(string heroStringId)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId)) return 0;

        // If the hero's OWN career cannot be resolved there is nothing to judge foreignness
        // against, so a career retired from XML costs the player nothing.
        if (_registry.GetCareer(careerId) == null) return 0;

        // Positive proof of foreignness only. An earlier draft built the allow-list from the
        // career's own groups and deleted everything outside it, which reads the same on a healthy
        // install and is catastrophic on a broken one: CareerConfigProvider.EnsureLoaded loads
        // taom_careers.xml and taom_career_choices.xml under SEPARATE try/catch blocks, so a
        // malformed choices file leaves every career resolvable and every GROUP empty. The
        // allow-list would collapse to the root choice and the repair would delete the player's
        // entire tree, permanently, on the next save. Owner-unknown now means keep.
        List<string> foreign = null;
        foreach (var choiceId in _dataService.GetChoiceIds(heroStringId))
        {
            var owner = _registry.GetOwningCareerId(choiceId);
            // IsNullOrEmpty, not != null: an empty owner id is a degenerate answer, not a career,
            // and must never be the thing that authorises a delete.
            if (string.IsNullOrEmpty(owner)) continue;
            if (string.Equals(owner, careerId, StringComparison.OrdinalIgnoreCase)) continue;

            (foreign ?? (foreign = new List<string>())).Add(choiceId);
        }

        if (foreign == null) return 0;

        foreach (var choiceId in foreign)
            _dataService.RemoveChoice(heroStringId, choiceId);
        return foreign.Count;
    }

    // internal for TAOM.Tests (InternalsVisibleTo): only TryRetrain and MigrateTreeVersion refund.
    internal int RefundAll(string heroStringId)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId)) return 0;

        var career = _registry.GetCareer(careerId);
        if (career == null) return 0;

        // Choices only. ClearCareer would also wipe the quest tier unlocks and the flags.
        var removed = 0;
        foreach (var choiceId in new List<string>(_dataService.GetChoiceIds(heroStringId)))
        {
            if (choiceId == career.RootChoiceId) continue;
            _dataService.RemoveChoice(heroStringId, choiceId);
            removed++;
        }

        if (!string.IsNullOrEmpty(career.RootChoiceId))
            _dataService.TryAddChoice(heroStringId, career.RootChoiceId, int.MaxValue);

        return removed;
    }

    public CareerRetrainResult TryRetrain(string heroStringId, int heroLevel)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        var career = string.IsNullOrEmpty(careerId) ? null : _registry.GetCareer(careerId);
        if (career == null) return CareerRetrainResult.NoCareer;

        if (!CareerChoiceRules.HoldsForkPick(career, _registry, _dataService.GetOrCreateData(heroStringId)))
            return CareerRetrainResult.NothingToRetrain;

        var cost = _registry.GetRetrainCost(heroLevel);
        if (!_purse.ChargeGold(cost))
            return CareerRetrainResult.NotEnoughGold;

        var removed = RefundAll(heroStringId);
        _passiveService.RefreshCache(_dataService, _registry);
        _logger.LogInfo($"CareerSystem: Retrained '{heroStringId}' ({careerId}) for {cost} gold, refunded {removed} choice(s)");
        return CareerRetrainResult.Done;
    }

    public int MigrateTreeVersion(string heroStringId)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId)) return 0;

        var career = _registry.GetCareer(careerId);
        if (career == null || career.TreeVersion <= 1) return 0;
        if (CareerTreeVersion.StoredVersion(_dataService.GetOrCreateData(heroStringId)) >= career.TreeVersion)
            return 0;

        if (!TreeLoaded(career))
        {
            _logger.LogWarning($"CareerSystem: career '{careerId}' is tree version {career.TreeVersion} but its groups did not load as expected; keeping '{heroStringId}' choices and setting no version flag");
            return 0;
        }

        var removed = RefundAll(heroStringId);
        CareerTreeVersion.Stamp(_dataService, heroStringId, career);
        _logger.LogInfo($"CareerSystem: '{careerId}' moved to tree version {career.TreeVersion}; refunded {removed} choice(s) for '{heroStringId}'");
        return removed;
    }

    public int MigrateAllTreeVersions(string playerHeroStringId)
    {
        // Every saved hero, not only the main hero: a co-op hero, or one the player takes over later,
        // keeps career data but is never the main hero at session launch, and nothing migrates it after.
        // Keys are copied first; the migration edits each hero's data, never the dictionary itself.
        var playerRefunded = 0;
        foreach (var heroStringId in new List<string>(_dataService.GetAllData().Keys))
        {
            var refunded = MigrateTreeVersion(heroStringId);
            if (heroStringId == playerHeroStringId)
                playerRefunded = refunded;
        }
        return playerRefunded;
    }

    private bool TreeLoaded(CareerDefinition career)
    {
        if (career.ChoiceGroupIds.Count == 0) return false;

        foreach (var groupId in career.ChoiceGroupIds)
        {
            if (_registry.GetGroup(groupId) == null) return false;
            if (_registry.GetChoicesForGroup(groupId).Count == 0) return false;
        }
        return true;
    }
}
