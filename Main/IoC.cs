using DryIoc;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Diplomacy;
using TAOM.Features.RaceAge;
using TAOM.Features.Execution;
using TAOM.Features.StartupResources;
using TAOM.Features.TroopProgression;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CustomBattles;
using TAOM.Features.TroopWeight;
using TAOM.Features.Warg;
using TAOM.Features.BattleBalance;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.SpecialResources;
using TAOM.Features.CareerSystem;

namespace TAOM;

public static class IoC
{
    private static IContainer _container;

    public static void Configure()
    {
        var container = new Container();

        container.RegisterInstance<IContainer>(container);

        RegisterCoreServices(container);
        RegisterLoggingServices(container);

        HeroRaceIoC.RegisterHeroRaceFeature(container);
        BannerInjectionIoC.RegisterBannerInjectionFeature(container);
        BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
        TroopProgressionIoC.RegisterTroopProgressionFeature(container);
        FactionMapIoC.RegisterFactionMapFeature(container);
        CharacterCreationIoC.RegisterCharacterCreationFeature(container);
        InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
        DiplomacyIoC.RegisterDiplomacyFeature(container);
        RaceAgeIoC.RegisterRaceAgeFeature(container);
        ExecutionIoC.RegisterExecutionFeature(container);
        StartupResourcesIoC.RegisterStartupResourcesFeature(container);
        TroopWeightIoC.RegisterTroopWeightFeature(container);
        AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
        WargIoC.RegisterWargFeature(container);
        CustomBattlesIoC.RegisterCustomBattlesFeature(container);
        BattleBalanceIoC.RegisterBattleBalanceFeature(container);
        MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
        ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
        SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
        ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
        TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        CareerSystemIoC.RegisterCareerSystemFeature(container);

        _container = container;

        // Post-registration initialization
        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
    }

    private static void RegisterCoreServices(IContainer container)
    {
        container.Register<IModulePathAdapter, ModulePathAdapter>(Reuse.Singleton);
        container.Register<IFaceGenAdapter, FaceGenAdapter>(Reuse.Singleton);
        container.Register<IPathService, PathService>(Reuse.Singleton);
        container.Register<IReflectionService, ReflectionService>(Reuse.Singleton);
        container.Register<IRaceManager, RaceManager>(Reuse.Singleton);
        container.Register<IHeroRosterAdapter, HeroRosterAdapter>(Reuse.Singleton);
        container.Register<IVolunteerContextAdapter, VolunteerContextAdapter>(Reuse.Singleton);
        container.Register<IMissionAdapterFactory, MissionAdapterFactory>(Reuse.Singleton);
        container.Register<IObjectManagerAdapter, ObjectManagerAdapter>(Reuse.Singleton);
    }

    private static void RegisterLoggingServices(IContainer container)
    {
        container.Register<IModLogger, FileLogger>(Reuse.Singleton);
    }

    public static T Resolve<T>()
    {
        return _container.Resolve<T>();
    }

    public static IEnumerable<T> ResolveAll<T>()
    {
        return _container.ResolveMany<T>();
    }

    public static void Dispose()
    {
        _container?.Dispose();
        _container = null;
    }
}
