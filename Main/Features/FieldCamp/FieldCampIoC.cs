using DryIoc;
using TAOM.Core.Domain;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FieldCamp;

public static class FieldCampIoC
{
    public static void RegisterFieldCampFeature(IContainer container)
    {
        container.Register<ICampSettingsProvider, CampSettingsProvider>(Reuse.Singleton);
        container.Register<ICampTerrainService, CampTerrainService>(Reuse.Singleton);
        container.Register<ICampAmbushService, CampAmbushService>(Reuse.Singleton);
        container.Register<ICampVisualService, CampVisualService>(Reuse.Singleton);

        // Singleton: owns the persisted camp book between the behavior's SyncData halves.
        container.Register<ICampService, CampService>(Reuse.Singleton);

        // The lookout widens the player's sight through TaomMapVisibilityModel's contributor
        // seam; a second AddModel would silently unseat the CareerSystem model that owns the slot.
        container.Register<IPartySpottingContributor, LookoutSpottingContributor>(Reuse.Singleton);

        var campService = container.Resolve<ICampService>();
        PartyNameplateCampIconPatch.Initialize(campService);
    }
}
