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

        // Tuning values are whole-number percentages (20 = 20%); convert to multiplier deltas.
        // AoE on nearby allies + self: mount speed (applies only if mounted), charge damage, damage.
        context.ApplyAllyCavalryBuff(
            mountSpeedBonus: tuning.MountSpeedBonus / 100f,
            chargeDamageBonus: tuning.ChargeDamageBonus / 100f,
            damageBonus: tuning.DamageBonus / 100f,
            radius: context.Radius,
            duration: context.Duration);
    }
}
