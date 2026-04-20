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

        // Tuning values are whole-number percentages (15 = 15%); convert to multiplier deltas.
        // AoE on nearby allies + self: speed, ranged damage, draw speed.
        context.ApplyAllyRangedBuff(
            speedBonus: tuning.SpeedBonus / 100f,
            damageBonus: tuning.RangedDamageBonus / 100f,
            drawSpeedBonus: tuning.DrawSpeedBonus / 100f,
            radius: context.Radius,
            duration: context.Duration);
    }
}
