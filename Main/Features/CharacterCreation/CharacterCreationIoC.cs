using System;
using DryIoc;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation.Hooks;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Features.CharacterCreation;

public static class CharacterCreationIoC
{
    public static void RegisterCharacterCreationFeature(IContainer container)
    {
        container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
        container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
        container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);

        Func<string> getSelectedCultureId = () =>
        {
            var activeState = Game.Current?.GameStateManager?.ActiveState as CharacterCreationState;
            return activeState?.CharacterCreationManager?.CharacterCreationContent?.SelectedCulture?.StringId;
        };

        container.RegisterDelegate<IOnGetRaceNames>(r =>
            new GetRaceNamesHook(
                r.Resolve<ICultureCreationDataProvider>(),
                r.Resolve<IModLogger>(),
                getSelectedCultureId),
            Reuse.Singleton);

        var raceHook = container.Resolve<IOnGetRaceNames>();
        FaceGen_GetRaceNames_Patch.Initialize(raceHook);
        CharacterTableau_SetRace_Patch.InitializeRaceMapper(raceHook.MapFilteredIndexToGlobalId);
    }
}
