using DryIoc;

namespace TAOM.Features.CombatMechanics;

public static class CombatMechanicsIoC
{
    public static void RegisterCombatMechanicsFeature(IContainer container)
    {
        container.Register<ICombatMechanicsConfigProvider, CombatMechanicsConfigProvider>(Reuse.Singleton);
        container.Register<ICombatMechanicsSettingsProvider, CombatMechanicsSettingsProvider>(Reuse.Singleton);
        container.Register<IRaceCombatModifiersResolver, RaceCombatModifiersResolver>(Reuse.Singleton);
        container.Register<ICrushThroughService, CrushThroughService>(Reuse.Singleton);
        container.Register<IChargeKnockdownService, ChargeKnockdownService>(Reuse.Singleton);
        container.Register<IChargeDamageService, ChargeDamageService>(Reuse.Singleton);
        container.Register<ICreatureCombatService, CreatureCombatService>(Reuse.Singleton);
        container.Register<IShieldPenetrationService, ShieldPenetrationService>(Reuse.Singleton);
        // The damage models' boundary to the four services above (#737; the campaign model and Custom Battle's twin, #788); SubModule resolves it once per game start for each.
        container.Register<Hooks.CombatMechanicsHooks>(Reuse.Transient);
    }
}
