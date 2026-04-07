using System;
using System.Collections.Generic;
using System.Reflection;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Mutations;

public class MutationService : IMutationService
{
    private readonly IMutationCalculatorRegistry _calculatorRegistry;
    private readonly IModLogger _logger;

    private static readonly Dictionary<string, PropertyInfo> PropertyCache
        = new Dictionary<string, PropertyInfo>();

    public MutationService(IMutationCalculatorRegistry calculatorRegistry, IModLogger logger)
    {
        _calculatorRegistry = calculatorRegistry;
        _logger = logger;
    }

    public AbilityTemplateData MutateAbility(
        AbilityTemplateData template,
        ICareerHeroAdapter hero,
        ICareerDataService dataService,
        ICareerRegistry registry)
    {
        if (template == null) return null;

        var clone = new AbilityTemplateData(template);
        var choiceIds = dataService.GetChoiceIds(hero.StringId);

        foreach (var choiceId in choiceIds)
        {
            var choice = registry.GetChoice(choiceId);
            if (choice == null) continue;

            foreach (var mutation in choice.Mutations)
            {
                if (!string.Equals(mutation.TargetTemplateId, template.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                ApplyMutation(clone, mutation, hero);
            }
        }

        // Also apply root choice mutations if not already in choiceIds
        var careerId = dataService.GetCareerStringId(hero.StringId);
        var career = careerId != null ? registry.GetCareer(careerId) : null;
        if (career != null && !string.IsNullOrEmpty(career.RootChoiceId))
        {
            var rootChoice = registry.GetChoice(career.RootChoiceId);
            if (rootChoice != null)
            {
                var alreadyIncluded = dataService.GetOrCreateData(hero.StringId).HasChoice(career.RootChoiceId);

                if (!alreadyIncluded)
                {
                    foreach (var mutation in rootChoice.Mutations)
                    {
                        if (!string.Equals(mutation.TargetTemplateId, template.Id, StringComparison.OrdinalIgnoreCase))
                            continue;
                        ApplyMutation(clone, mutation, hero);
                    }
                }
            }
        }

        return clone;
    }

    private void ApplyMutation(AbilityTemplateData target, MutationDefinition mutation, ICareerHeroAdapter hero)
    {
        var prop = GetCachedProperty(mutation.PropertyName);
        if (prop == null)
        {
            _logger.LogWarning($"Mutation: property '{mutation.PropertyName}' not found on AbilityTemplateData");
            return;
        }

        if (prop.PropertyType != typeof(float))
        {
            _logger.LogWarning($"Mutation: property '{mutation.PropertyName}' is not float (is {prop.PropertyType.Name})");
            return;
        }

        var currentValue = (float)prop.GetValue(target);
        var calculatedValue = _calculatorRegistry.Calculate(
            mutation.CalculatorId, currentValue, hero, new MutationParams(mutation.Params));

        // The calculator already handles the operation (flat adds, multiply multiplies, etc.)
        // So the calculated value IS the final value.
        float newValue = calculatedValue;

        prop.SetValue(target, newValue);
    }

    private static PropertyInfo GetCachedProperty(string propertyName)
    {
        if (PropertyCache.TryGetValue(propertyName, out var cached))
            return cached;

        var prop = typeof(AbilityTemplateData).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        PropertyCache[propertyName] = prop;
        return prop;
    }
}
