using DryIoc;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Diplomacy;
using TAOM.Features.RaceAge;
using TAOM.Features.TroopProgression;

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
        TroopProgressionIoC.RegisterTroopProgressionFeature(container);
        FactionMapIoC.RegisterFactionMapFeature(container);
        CharacterCreationIoC.RegisterCharacterCreationFeature(container);
        InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
        DiplomacyIoC.RegisterDiplomacyFeature(container);
        RaceAgeIoC.RegisterRaceAgeFeature(container);

        _container = container;
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
