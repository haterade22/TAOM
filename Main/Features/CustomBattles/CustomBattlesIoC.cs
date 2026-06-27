using DryIoc;
using TAOM.Features.CustomBattles.Config;
using TAOM.Features.CustomBattles.Hooks;

namespace TAOM.Features.CustomBattles;

public static class CustomBattlesIoC
{
    public static void RegisterCustomBattlesFeature(IContainer container)
    {
        container.Register<ICustomBattleCommandersProvider, CustomBattleCommandersProvider>(Reuse.Singleton);
        container.Register<ICustomBattleService, CustomBattleService>(Reuse.Singleton);
        container.Register<ISideCommanderFilter, SideCommanderFilter>(Reuse.Singleton);
        container.Register<IOnGetCustomBattleCommanders, CustomBattleCommandersHook>(Reuse.Transient);
        container.Register<IOnGetCustomBattleFactions, CustomBattleFactionsHook>(Reuse.Transient);
        container.Register<IOnGetDefaultTroopOfFormation, CustomBattleTroopHook>(Reuse.Transient);
    }

    public static void InitializeHooks(
        IOnGetCustomBattleCommanders commandersHook,
        IOnGetCustomBattleFactions factionsHook,
        IOnGetDefaultTroopOfFormation troopHook,
        ISideCommanderFilter sideCommanderFilter,
        Core.Logging.IModLogger logger)
    {
        CommanderSelectorRebuilder.Initialize(logger);
        CustomBattleData_Characters_Patch.Initialize(commandersHook, logger);
        CustomBattleData_Factions_Patch.Initialize(factionsHook, logger);
        CustomBattleHelper_Troop_Patch.Initialize(troopHook, logger);
        CustomBattleSideVM_Constructor_Patch.Initialize(logger);
        CustomBattleSideVM_OnCultureSelection_Patch.Initialize(sideCommanderFilter, logger);
        CustomBattleSideVM_RefreshValues_Patch.Initialize(sideCommanderFilter, logger);
        BannerlordMissions_CustomBattle_Patch.Initialize(logger);
        BannerlordMissions_Siege_Patch.Initialize(logger);
    }
}
