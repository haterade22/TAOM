using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class PlayerEquipmentService : IPlayerEquipmentService
{
    private readonly IPlayerEquipmentAdapter _adapter;
    private readonly IModLogger _logger;

    public PlayerEquipmentService(IPlayerEquipmentAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public void ApplyPlayerStartingEquipment(string cultureId, string titleType, bool isFemale, string playerHeroId)
    {
        if (string.IsNullOrEmpty(cultureId))
        {
            _logger.LogWarning("PlayerEquipmentService: cultureId is null or empty — skipping equipment apply");
            return;
        }

        if (string.IsNullOrEmpty(titleType))
        {
            _logger.LogWarning($"PlayerEquipmentService: titleType is null or empty for culture '{cultureId}' — skipping equipment apply");
            return;
        }

        if (string.IsNullOrEmpty(playerHeroId))
        {
            _logger.LogWarning($"PlayerEquipmentService: playerHeroId is null or empty for culture '{cultureId}' — skipping equipment apply");
            return;
        }

        var rosterId = PlayerEquipmentRosterIds.Build(cultureId, titleType, isFemale);
        var result = _adapter.ApplyRosterToPlayer(rosterId, playerHeroId);

        switch (result)
        {
            case PlayerEquipmentApplyResult.Success:
                _logger.LogInfo($"PlayerEquipmentService: applied roster '{rosterId}' to player ({cultureId}/{titleType})");
                break;
            case PlayerEquipmentApplyResult.RosterNotFound:
                _logger.LogWarning($"PlayerEquipmentService: equipment roster '{rosterId}' not found — no equipment applied");
                break;
            case PlayerEquipmentApplyResult.NoSuitableEquipment:
                _logger.LogWarning($"PlayerEquipmentService: roster '{rosterId}' has no battle or civilian equipment — no equipment applied");
                break;
            case PlayerEquipmentApplyResult.HeroNotFound:
                _logger.LogError($"PlayerEquipmentService: hero '{playerHeroId}' not found — equipment apply failed");
                break;
        }
    }
}
