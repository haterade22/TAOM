using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// Registers Encounter Lines through <see cref="EncounterLinesModule"/>. The adapter is registered as its concrete type and
/// mapped to its interface, so the conversation tags' describe delegate and the service share one instance.
/// </summary>
public static class EncounterLinesIoC
{
    public static void RegisterEncounterLinesFeature(IRegistrator registrator)
    {
        registrator.Register<IEncounterLinesSettingsProvider, EncounterLinesSettingsProvider>(Reuse.Singleton);
        registrator.Register<EncounterLinesAdapter>(Reuse.Singleton);
        registrator.RegisterDelegate<IEncounterLinesAdapter>(r => r.Resolve<EncounterLinesAdapter>(), Reuse.Singleton);
        registrator.Register<IEncounterGroupsConfigProvider, EncounterGroupsConfigProvider>(Reuse.Singleton);
        registrator.Register<EncounterLinesService>(Reuse.Singleton);
        registrator.RegisterDelegate<IEncounterBucketSource>(r => r.Resolve<EncounterLinesService>(), Reuse.Singleton);
    }
}
