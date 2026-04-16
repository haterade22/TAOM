namespace TAOM.Features.CareerSystem.Abilities.Executors;

public sealed class InfantryAbilityExecutor : ICareerAbilityEffectExecutor
{
    private readonly ICareerConfigProvider _configProvider;

    public string CareerId { get; }

    public InfantryAbilityExecutor(string careerId, ICareerConfigProvider configProvider)
    {
        CareerId = careerId;
        _configProvider = configProvider;
    }

    public void Execute(IAbilityExecutionContext context)
    {
        var tuning = _configProvider.GetAbilityTuning().Infantry;
        // Convert flat percentage values to multiplier deltas (15 → 0.15)
        // Use context.Radius (from mutated ability template) so choice-tree radius upgrades apply
        context.ApplyAllyBuff(tuning.DamageBonus / 100f, tuning.DamageReduction / 100f, context.Radius, context.Duration);
    }
}
