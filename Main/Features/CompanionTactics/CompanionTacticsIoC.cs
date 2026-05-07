using DryIoc;
using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.Roles;

namespace TAOM.Features.CompanionTactics;

/// <summary>
/// DryIoc registrations for CompanionTactics. All services are <see cref="Reuse.Singleton"/>
/// — they hold per-mission state (stance manager, preset list, role cache) that must persist
/// across patches in the same battle. Adapter snapshots (HeroCombatAdapter, AgentCombatAdapter,
/// BattleEquipmentSnapshot) are NOT registered: they are constructed at the boundary where a
/// sealed Hero/Agent is in scope, and are not injected.
/// </summary>
public static class CompanionTacticsIoC
{
    public static void RegisterCompanionTacticsFeature(IContainer container)
    {
        container.Register<ICompanionTacticsSettingsProvider, CompanionTacticsSettingsProvider>(Reuse.Singleton);

        // Roles
        container.Register<ICompanionRoleService, CompanionRoleService>(Reuse.Singleton);
        container.Register<IRoleTooltipDecorator, RoleTooltipDecorator>(Reuse.Singleton);

        // FormationPresets
        container.Register<IFormationPresetService, FormationPresetService>(Reuse.Singleton);
        container.Register<IHeroAutoAssigner, HeroAutoAssigner>(Reuse.Singleton);
        container.Register<IOrderOfBattleVMTracker, OrderOfBattleVMTracker>(Reuse.Singleton);
        container.Register<IOOBOverlayService, OOBOverlayService>(Reuse.Singleton);

        // BattleActionBar
        container.Register<IBattleActionBarService, BattleActionBarService>(Reuse.Singleton);
        container.Register<ITroopStanceManager, TroopStanceManager>(Reuse.Singleton);
        container.Register<IFormationCompositionAnalyzer, FormationCompositionAnalyzer>(Reuse.Singleton);
    }
}
