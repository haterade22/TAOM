using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Abilities;

public class CareerAbilityEffectRegistry
{
    private static readonly ICareerAbilityEffectExecutor NoOp = new NoOpExecutor();

    private readonly Dictionary<string, ICareerAbilityEffectExecutor> _executors
        = new Dictionary<string, ICareerAbilityEffectExecutor>();

    public void Register(ICareerAbilityEffectExecutor executor)
    {
        _executors[executor.CareerId] = executor;
    }

    public ICareerAbilityEffectExecutor GetExecutor(string careerId)
    {
        return _executors.TryGetValue(careerId, out var executor) ? executor : NoOp;
    }

    private sealed class NoOpExecutor : ICareerAbilityEffectExecutor
    {
        public string CareerId => "__noop__";
        public void Execute(IAbilityExecutionContext context) { }
    }
}
