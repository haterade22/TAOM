using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Models;

/// <summary>
/// Custom Battle (and the editor's test battle): <c>CustomBattleBannerBearersModel</c> with TAOM's race gate on top,
/// so a troll never carries or fetches a standard there either. <see cref="TaomBattleBannerBearersModel"/> is
/// registered on the campaign starter only, and Custom Battle builds its own model off a BasicGameStarter (v1.4.8
/// CustomGame.InitializeGameModels), so before this model the vanilla gate alone ran there. Only the race gate is
/// added: Custom Battle keeps its own bearer count, tiers and formation rules.
/// </summary>
public class TaomCustomBattleBannerBearersModel : CustomBattleBannerBearersModel
{
    private readonly IBannerBearerService _service;

    public TaomCustomBattleBannerBearersModel(IBannerBearerService service)
    {
        _service = service;
    }

    // Same polarity as the campaign model: the disabled feature defers to vanilla, and -1 (no character) is an
    // invalid race id, which the service fails closed.
    public override bool CanAgentBecomeBannerBearer(Agent agent) =>
        base.CanAgentBecomeBannerBearer(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1);

    // BannerBearerLogic.FindBestSearcherForBanner picks the nearest formation member passing ONLY this check to
    // fetch a banner dropped on the ground; CanAgentBecomeBannerBearer is never consulted for that path.
    public override bool CanAgentPickUpAnyBanner(Agent agent) =>
        base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1);
}
