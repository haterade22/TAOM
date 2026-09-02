using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem;

public class CareerLifecycleService : ICareerLifecycleService
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerCreationHandler _creationHandler;
    private readonly IModLogger _logger;

    public CareerLifecycleService(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerCreationHandler creationHandler,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _creationHandler = creationHandler;
        _logger = logger;
    }

    public bool AssignFallbackCareerIfMissing(string heroStringId, string cultureId)
    {
        if (string.IsNullOrEmpty(heroStringId)) return false;
        if (_dataService.HasCareer(heroStringId)) return false;

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
}
