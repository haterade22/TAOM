using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.CharacterCreation;

public static class CharacterCreationIoC
{
    public static void RegisterCharacterCreationFeature(IContainer container)
    {
        container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
        container.Register<ICultureRaceFilterService, CultureRaceFilterService>(Reuse.Singleton);
        container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
        container.Register<IEquipmentRosterProvider, EquipmentRosterProvider>(Reuse.Singleton);
        container.Register<ICareerMenuDataProvider, CareerMenuDataProvider>(Reuse.Singleton);
        container.Register<ICareerMenuService, CareerMenuService>(Reuse.Singleton);
        container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);
        container.Register<INarrativeHorseGuardService, NarrativeHorseGuardService>(Reuse.Singleton);
        container.Register<ICCBodyPropertiesProvider, CCBodyPropertiesProvider>(Reuse.Singleton);
        container.Register<IPlayerBodyPropertiesAdapter, PlayerBodyPropertiesAdapter>(Reuse.Singleton);
        container.Register<ICCBodyPropertiesService, CCBodyPropertiesService>(Reuse.Singleton);
        container.Register<IPlayerEquipmentAdapter, PlayerEquipmentAdapter>(Reuse.Singleton);
        container.Register<IPlayerEquipmentService, PlayerEquipmentService>(Reuse.Singleton);
    }
}
