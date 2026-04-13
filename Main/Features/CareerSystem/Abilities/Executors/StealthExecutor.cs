namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class StealthExecutor : ICareerAbilityEffectExecutor
{
    private const float SpeedMultiplier = 1.35f;

    public string CareerId => "ranger_of_ithilien";

    public void Execute(IAbilityExecutionContext context)
    {
        context.ApplySpeedBuff(SpeedMultiplier, context.Duration);
        context.ApplyStealthMode(context.Duration);
    }
}
