namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class CavalryAbilityExecutor : ICareerAbilityEffectExecutor
{
    private readonly ICareerConfigProvider _configProvider;

    public string CareerId { get; }

    public CavalryAbilityExecutor(string careerId, ICareerConfigProvider configProvider)
    {
        CareerId = careerId;
        _configProvider = configProvider;
    }

    public void Execute(IAbilityExecutionContext context)
    {
        var tuning = _configProvider.GetAbilityTuning().Cavalry;

        // Mount speed: percentage → multiplier delta (20 → 0.20)
        context.ApplyMountSpeedBuff(tuning.MountSpeedBonus / 100f, context.Duration);

        // Charge damage: percentage → multiplier delta (25 → 0.25)
        context.ApplyChargeDamageBuff(tuning.ChargeDamageBonus / 100f, context.Duration);

        // Damage: percentage → multiplier (10 → 1.10)
        context.ApplyDamageBuff(1f + tuning.DamageBonus / 100f, context.Duration);
    }
}
