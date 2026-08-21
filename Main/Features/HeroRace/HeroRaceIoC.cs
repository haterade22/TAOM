using DryIoc;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Features.HeroRace;

public static class HeroRaceIoC
{
    public static void RegisterHeroRaceFeature(IContainer container)
    {
        // Single owner of both race-framing configs. Singleton because the rows it hands out are
        // the live instances the in-game tuner edits — a transient store would silently discard
        // every nudge.
        container.Register<IRacePositionStore, RacePositionStore>(Reuse.Singleton);

        container.Register<IHeroRaceSettingsProvider, HeroRaceSettingsProvider>(Reuse.Singleton);

        container.Register<ICharacterSpawnerService, CharacterSpawnerService>(Reuse.Singleton);

        // 3D tableau framing (Patch72). Until this existed the avatar offsets were loaded, parsed
        // and never applied: the service that consumed them was registered but never invoked.
        container.Register<ITableauPositionService, TableauPositionService>(Reuse.Singleton);
        var tableauPositionService = container.Resolve<ITableauPositionService>();
        CharacterTableau_RefreshCharacterTableau_PositionPatch.Initialize(
            tableauPositionService,
            container.Resolve<IModLogger>());

        container.Register<IOnFaceGenGetBaseMonsterFromRace, EyeHeightAdjustmentHook>(Reuse.Singleton);

        var eyeHeightHook = container.Resolve<IOnFaceGenGetBaseMonsterFromRace>();
        FaceGen_GetBaseMonsterFromRace_Patch.Initialize(eyeHeightHook);

        container.Register<IRacePersistenceService, RacePersistenceService>(Reuse.Singleton);

        // Save/Load hero preview crash-stop (issue #295 class): coerce custom races to the human base
        // in BasicCharacterTableau so the agentless static-morph build can't AV on a morph-less head.
        // Per-race empirical allow-list (by name, via IRaceManager): render-verified races pass
        // through true-to-race (uruk, 2026-07-02); dwarf is proven unsafe and stays coerced.
        container.Register<IBasicTableauRaceGuard, BasicTableauRaceGuard>(Reuse.Singleton);
        var basicTableauRaceGuard = container.Resolve<IBasicTableauRaceGuard>();
        BasicCharacterTableau_RefreshCharacterTableau_Patch.Initialize(basicTableauRaceGuard);
    }
}
