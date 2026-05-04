using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public class CareerAbilityService : ICareerAbilityService
{
    private readonly ICareerConfigProvider _config;
    private readonly IModLogger _logger;
    private readonly Dictionary<string, CareerAbility> _abilities = new Dictionary<string, CareerAbility>();

    public CareerAbilityService(ICareerConfigProvider config, IModLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService)
    {
        if (_abilities.TryGetValue(heroStringId, out var existing))
            return existing;

        var careerId = dataService.GetCareerStringId(heroStringId);
        if (string.IsNullOrEmpty(careerId))
        {
            _logger.LogDebug($"CareerSystem: GetOrCreateAbility — no career for hero '{heroStringId}'");
            return null;
        }

        var career = registry.GetCareer(careerId);
        if (career == null)
        {
            _logger.LogWarning($"CareerSystem: GetOrCreateAbility — career '{careerId}' not found in registry for hero '{heroStringId}'");
            return null;
        }

        var cooldownSeconds = _config.GetAbilityTuning().Global.CooldownSeconds;

        var ability = new CareerAbility(
            career.AbilityTemplateId,
            ChargeType.CooldownOnly,
            maxCharge: 0f,
            cooldownDuration: cooldownSeconds);

        _abilities[heroStringId] = ability;
        _logger.LogInfo($"CareerSystem: Created ability for hero '{heroStringId}' — template='{career.AbilityTemplateId}', cooldownSeconds={cooldownSeconds}");
        return ability;
    }

    public void Tick(string heroStringId, float dt)
    {
        if (_abilities.TryGetValue(heroStringId, out var ability))
            ability.Tick(dt);
    }

    public bool IsAbilityReady(string heroStringId)
    {
        return _abilities.TryGetValue(heroStringId, out var ability) && ability.IsReady;
    }

    public float GetCooldownRemaining(string heroStringId)
    {
        return _abilities.TryGetValue(heroStringId, out var ability) ? ability.CooldownRemaining : 0f;
    }

    public void ActivateAbility(string heroStringId)
    {
        if (_abilities.TryGetValue(heroStringId, out var ability))
        {
            ability.Activate();
            _logger.LogInfo($"CareerSystem: Ability activated for hero '{heroStringId}'");
        }
        else
        {
            _logger.LogWarning($"CareerSystem: ActivateAbility — no ability found for hero '{heroStringId}'");
        }
    }

    public void ClearAll()
    {
        _abilities.Clear();
    }
}
