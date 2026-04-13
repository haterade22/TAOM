namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class StampedeExecutor : ICareerAbilityEffectExecutor
{
    private const float DamageMultiplier = 1.4f;
    private const float MoraleMagnitude = 20f;

    public string CareerId => "knight_of_belfalas";

    public void Execute(IAbilityExecutionContext context)
    {
        context.ApplyDamageBuff(DamageMultiplier, context.Duration);
        context.ApplyMoraleBurst(context.Radius, MoraleMagnitude);
    }
}
