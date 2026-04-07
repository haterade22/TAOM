using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.Mutations;

public class MutationCalculatorRegistry : IMutationCalculatorRegistry
{
    private readonly Dictionary<string, Func<float, ICareerHeroAdapter, MutationParams, float>> _calculators
        = new Dictionary<string, Func<float, ICareerHeroAdapter, MutationParams, float>>();
    private readonly IModLogger _logger;

    public MutationCalculatorRegistry(IModLogger logger)
    {
        _logger = logger;
    }

    public void Register(string calculatorId, Func<float, ICareerHeroAdapter, MutationParams, float> calculator)
    {
        _calculators[calculatorId] = calculator;
    }

    public float Calculate(string calculatorId, float baseValue, ICareerHeroAdapter hero, MutationParams parameters)
    {
        if (!_calculators.TryGetValue(calculatorId, out var calculator))
        {
            _logger.LogWarning($"MutationCalc: unknown calculator '{calculatorId}', returning base value");
            return baseValue;
        }
        return calculator(baseValue, hero, parameters);
    }

    public bool HasCalculator(string calculatorId)
    {
        return _calculators.ContainsKey(calculatorId);
    }
}
