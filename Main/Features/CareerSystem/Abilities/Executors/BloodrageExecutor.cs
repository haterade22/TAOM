namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class BloodrageExecutor : ICareerAbilityEffectExecutor
{
    private const float DamageMultiplier = 1.5f;
    private const float ResistanceMultiplier = 1.3f;

    public string CareerId => "black_uruk_captain";

    public void Execute(IAbilityExecutionContext context)
    {
        context.ApplyDamageBuff(DamageMultiplier, context.Duration);
        context.ApplyResistanceBuff(ResistanceMultiplier, context.Duration);
    }
}
