using System;
using TAOM.Adapters;

namespace TAOM.Features.CareerSystem.Mutations;

public interface IMutationCalculatorRegistry
{
    void Register(string calculatorId, Func<float, ICareerHeroAdapter, MutationParams, float> calculator);
    float Calculate(string calculatorId, float baseValue, ICareerHeroAdapter hero, MutationParams parameters);
    bool HasCalculator(string calculatorId);
}
