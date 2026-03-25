namespace TAOM.Features.Diplomacy.Hooks;

public class PeaceActionHook : IOnPeaceAction
{
    private readonly IWarOfTheRingService _wotrService;

    public PeaceActionHook(IWarOfTheRingService wotrService)
    {
        _wotrService = wotrService;
    }

    public bool ShouldPreventPeace(string factionAId, string factionBId)
    {
        return _wotrService.IsWarOfTheRingActive
               && _wotrService.ShouldBlockPeace(factionAId, factionBId);
    }
}
