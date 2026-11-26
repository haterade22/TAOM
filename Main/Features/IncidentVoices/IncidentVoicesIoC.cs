using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.IncidentVoices;

/// <summary>Registers Incident Voices through <see cref="IncidentVoicesModule"/>. The group map comes from Encounter Lines' registration.</summary>
public static class IncidentVoicesIoC
{
    public static void RegisterIncidentVoicesFeature(IRegistrator registrator)
    {
        registrator.Register<IIncidentCatalogProvider, IncidentCatalogProvider>(Reuse.Singleton);
        registrator.Register<IIncidentVoiceAdapter, IncidentVoiceAdapter>(Reuse.Singleton);
        registrator.Register<IncidentVoiceService>(Reuse.Singleton);
    }
}
