using DryIoc;

namespace TAOM.Features.PlayerPossession;

public static class PlayerPossessionIoC
{
    public static void RegisterPlayerPossessionFeature(IContainer container)
    {
        // Singleton is REQUIRED, not a default: the character-creation choices are recorded in one
        // campaign and consumed in the campaign that replaces it, so anything campaign-scoped would
        // be disposed in between and lose exactly the data this feature carries.
        container.Register<IPlayerPossessionService, PlayerPossessionService>(Reuse.Singleton);
        container.Register<IJoinReconciliationService, JoinReconciliationService>(Reuse.Singleton);
    }
}
