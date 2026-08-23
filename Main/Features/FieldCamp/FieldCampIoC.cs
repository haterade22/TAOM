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

        // NOTE deliberately NO eager Resolve here. CampService's constructor materializes the
        // ICampOverlayContributor collection, so resolving it before every feature has registered
        // bakes the collection EMPTY and Refuge's camp-block contributor never applies (review
        // round A / Codex round 1, same finding). All eager patch initialisation now lives in
        // IoC.InitializePatchStatics, which runs after the last registration.
    }

    internal static void InitializePatchStatics(IContainer container)
    {
        PartyNameplateCampIconPatch.Initialize(container.Resolve<ICampService>());
    }
}
