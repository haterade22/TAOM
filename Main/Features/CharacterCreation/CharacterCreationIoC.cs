using DryIoc;

namespace TAOM.Features.CharacterCreation;

public static class CharacterCreationIoC
{
    public static void RegisterCharacterCreationFeature(IContainer container)
    {
        container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
        container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
        container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);
    }
}
