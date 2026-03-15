using DryIoc;
using TAOM.Features.CharacterCreation.Hooks;

namespace TAOM.Features.CharacterCreation;

public static class CharacterCreationIoC
{
    public static void RegisterCharacterCreationFeature(IContainer container)
    {
        container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
        container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
        container.Register<IEquipmentRosterProvider, EquipmentRosterProvider>(Reuse.Singleton);
        container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);
        container.Register<IOnGetRaceNames, GetRaceNamesHook>(Reuse.Singleton);
    }
}
