using DryIoc;
using TAOM.Features.CustomBattles.Hooks;

namespace TAOM.Features.CustomBattles;

public static class CustomBattlesIoC
{
    public static void RegisterCustomBattlesFeature(IContainer container)
    {
        container.Register<ICustomBattleService, CustomBattleService>(Reuse.Singleton);
        container.Register<IOnGetCustomBattleCommanders, CustomBattleCommandersHook>(Reuse.Transient);
        container.Register<IOnGetCustomBattleFactions, CustomBattleFactionsHook>(Reuse.Transient);
        container.Register<IOnGetDefaultTroopOfFormation, CustomBattleTroopHook>(Reuse.Transient);
    }

    public static void InitializeHooks(
        IOnGetCustomBattleCommanders commandersHook,
        IOnGetCustomBattleFactions factionsHook,
        IOnGetDefaultTroopOfFormation troopHook,
        Core.Logging.IModLogger logger)
    {
        CustomBattleData_Characters_Patch.Initialize(commandersHook, logger);
        CustomBattleData_Factions_Patch.Initialize(factionsHook, logger);
        CustomBattleHelper_Troop_Patch.Initialize(troopHook, logger);
        BannerlordMissions_CustomBattle_Patch.Initialize(logger);
        BannerlordMissions_Siege_Patch.Initialize(logger);
    }
}
