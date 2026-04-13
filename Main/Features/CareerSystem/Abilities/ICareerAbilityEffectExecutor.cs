namespace TAOM.Features.CareerSystem.Abilities;

public interface ICareerAbilityEffectExecutor
{
    string CareerId { get; }
    void Execute(IAbilityExecutionContext context);
}
