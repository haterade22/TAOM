using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;

namespace TAOM.Features.CharacterCreation;

public sealed class CareerStartingEquipmentService : ICareerStartingEquipmentService
{
    private readonly IPlayerEquipmentAdapter _adapter;
    private readonly ICareerArchetypeService _archetypeService;
    private readonly IModLogger _logger;

    public CareerStartingEquipmentService(
        IPlayerEquipmentAdapter adapter,
        ICareerArchetypeService archetypeService,
        IModLogger logger)
    {
        _adapter = adapter;
        _archetypeService = archetypeService;
        _logger = logger;
    }

    public void ApplyCareerStartingEquipment(string cultureId, string careerId, bool isFemale, string playerHeroId)
    {
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("CareerStartingEquipmentService: cultureId is null or empty — skipping career equipment");
            return;
        }
        if (string.IsNullOrEmpty(careerId))
        {
            _logger.LogInfo("CareerStartingEquipmentService: no career selected — leaving culture-default equipment in place");
            return;
        }
        if (string.IsNullOrEmpty(playerHeroId))
        {
            _logger.LogWarning($"CareerStartingEquipmentService: playerHeroId is null or empty for career '{careerId}' — skipping");
            return;
        }

        if (!_archetypeService.TryGetArchetype(careerId, out var archetype))
        {
            _logger.LogInfo($"CareerStartingEquipmentService: no archetype mapped for career '{careerId}' — leaving culture-default in place");
            return;
        }

        var rosterId = CareerEquipmentRosterIds.Build(cultureId, archetype, isFemale);
        var result = _adapter.ApplyRosterToPlayer(rosterId, playerHeroId);

        switch (result)
        {
            case PlayerEquipmentApplyResult.Success:
                _logger.LogInfo($"CareerStartingEquipmentService: applied roster '{rosterId}' to player ({cultureId}/{archetype}/{(isFemale ? "f" : "m")})");
                break;
            case PlayerEquipmentApplyResult.RosterNotFound:
                _logger.LogWarning($"CareerStartingEquipmentService: roster '{rosterId}' not found — leaving culture-default in place");
                break;
            case PlayerEquipmentApplyResult.NoSuitableEquipment:
                _logger.LogWarning($"CareerStartingEquipmentService: roster '{rosterId}' has no battle or civilian equipment — leaving culture-default in place");
                break;
            case PlayerEquipmentApplyResult.HeroNotFound:
                _logger.LogError($"CareerStartingEquipmentService: hero '{playerHeroId}' not found — career equipment apply failed");
                break;
        }
    }
}
