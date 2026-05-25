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
using TAOM.Features.Encyclopedia;
using TAOM.Features.RaceAge;
using TAOM.Features.Execution;
using TAOM.Features.StartupResources;
using TAOM.Features.TroopProgression;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CustomBattles;
using TAOM.Features.TroopWeight;
using TAOM.Features.Warg;
// DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
// using TAOM.Features.Spider;
using TAOM.Features.BattleBalance;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.Messengers;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.SpecialResources;
using TAOM.Features.CareerSystem;
using TAOM.Features.SettlementGuards;
using TAOM.Features.NamedCompanions;
using TAOM.Features.RevoltTuning;
using TAOM.Features.SiegeDismount;
using TAOM.Features.MixedFormations;
using TAOM.Features.SmartCavalryAI;
using TAOM.Features.QuickActions;
using TAOM.Features.CompanionTactics;
using TAOM.Features.FiefManagement;
using TAOM.Features.EquipPresets;
using TAOM.Features.EditorCacheRebuild;
using TAOM.Features.CulturalFeats;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.MissionDiagnostic;
using TAOM.Features.CrashReport;
using TAOM.Features.SettlementNameplateFade;

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
        Features.Arena.ArenaIoC.RegisterArenaFeature(container);
        FactionMapIoC.RegisterFactionMapFeature(container);
        CharacterCreationIoC.RegisterCharacterCreationFeature(container);
        InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
        DiplomacyIoC.RegisterDiplomacyFeature(container);
        RaceAgeIoC.RegisterRaceAgeFeature(container);
        ExecutionIoC.RegisterExecutionFeature(container);
        EncyclopediaIoC.RegisterEncyclopediaFeature(container);
        StartupResourcesIoC.RegisterStartupResourcesFeature(container);
        TroopWeightIoC.RegisterTroopWeightFeature(container);
        AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
        WargIoC.RegisterWargFeature(container);
        // DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
        // SpiderIoC.RegisterSpiderFeature(container);
        CustomBattlesIoC.RegisterCustomBattlesFeature(container);
        BattleBalanceIoC.RegisterBattleBalanceFeature(container);
        MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
        ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
        SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
        ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
        TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        CareerSystemIoC.RegisterCareerSystemFeature(container);
        SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
        NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
        RevoltTuningIoC.RegisterRevoltTuningFeature(container);
        SiegeDismountIoC.RegisterSiegeDismountFeature(container);
        MixedFormationsIoC.RegisterMixedFormationsFeature(container);
        SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
        QuickActionsIoC.RegisterQuickActionsFeature(container);
        MessengerIoC.RegisterMessengerFeature(container);
        EquipPresetsIoC.RegisterEquipPresetsFeature(container);
        CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
        FiefManagementIoC.RegisterFiefManagementFeature(container);
        EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);
        CulturalFeatsIoC.RegisterCulturalFeatsFeature(container);
        CultureMarketplaceIoC.RegisterCultureMarketplaceFeature(container);
        MissionDiagnosticIoC.RegisterMissionDiagnosticFeature(container);
        CrashReportIoC.RegisterCrashReportFeature(container);
        NameplateFadeIoC.RegisterSettlementNameplateFadeFeature(container);

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
        // Phase 9b #148 P2 — IVolunteerContextAdapter moved to TroopProgressionIoC for cohesion
        // (only consumer is TaomVolunteerModel inside that feature).
        container.Register<IMissionAdapterFactory, MissionAdapterFactory>(Reuse.Singleton);
        container.Register<IObjectManagerAdapter, ObjectManagerAdapter>(Reuse.Singleton);
        container.Register<ICampaignSessionAdapter, CampaignSessionAdapter>(Reuse.Singleton);
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
