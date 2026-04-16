namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class RangedAbilityExecutor : ICareerAbilityEffectExecutor
{
    private readonly ICareerConfigProvider _configProvider;

    public string CareerId { get; }

    public RangedAbilityExecutor(string careerId, ICareerConfigProvider configProvider)
    {
        CareerId = careerId;
        _configProvider = configProvider;
    }

    public void Execute(IAbilityExecutionContext context)
    {
        var tuning = _configProvider.GetAbilityTuning().Ranged;

        // All tuning values are whole-number percentages (15 = 15%), convert to engine scale

        // Speed: percentage → multiplier (15 → 1.15)
        context.ApplySpeedBuff(1f + tuning.SpeedBonus / 100f, context.Duration);

        // Ranged damage: percentage → multiplier (20 → 1.20)
        context.ApplyDamageBuff(1f + tuning.RangedDamageBonus / 100f, context.Duration);

        // Draw speed: percentage → multiplier delta (20 → 0.20)
        context.ApplyDrawSpeedBuff(tuning.DrawSpeedBonus / 100f, context.Duration);
    }
}
