using DryIoc;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Features.HeroRace;

public static class HeroRaceIoC
{
    public static void RegisterHeroRaceFeature(IContainer container)
    {
        container.Register<IRacePositionConfigurationService, RacePositionConfigurationService>(Reuse.Singleton);

        container.Register<ICharacterTableauService, CharacterTableauService>(Reuse.Singleton);

        container.Register<ICharacterSpawnerService, CharacterSpawnerService>(Reuse.Singleton);

        container.Register<IOnFaceGenGetBaseMonsterFromRace, EyeHeightAdjustmentHook>(Reuse.Singleton);

        var eyeHeightHook = container.Resolve<IOnFaceGenGetBaseMonsterFromRace>();
        FaceGen_GetBaseMonsterFromRace_Patch.Initialize(eyeHeightHook);

        container.Register<IRacePersistenceService, RacePersistenceService>(Reuse.Singleton);

        // Save/Load hero preview crash-stop (issue #295 class): coerce custom races to the human base
        // in BasicCharacterTableau so the agentless static-morph build can't AV on a morph-less head.
        container.Register<IBasicTableauRaceGuard, BasicTableauRaceGuard>(Reuse.Singleton);
        var basicTableauRaceGuard = container.Resolve<IBasicTableauRaceGuard>();
        BasicCharacterTableau_RefreshCharacterTableau_Patch.Initialize(basicTableauRaceGuard);
    }
}
