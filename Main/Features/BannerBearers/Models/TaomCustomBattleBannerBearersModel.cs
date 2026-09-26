using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Models;

/// <summary>
/// Custom Battle (and the editor's test battle): <c>CustomBattleBannerBearersModel</c> with TAOM's race gate on top,
/// so a troll never carries a standard there either. <see cref="TaomBattleBannerBearersModel"/> is registered on the
/// campaign starter only, and Custom Battle builds its own model off a BasicGameStarter, so until 2026-09-25 the
/// vanilla gate alone ran there and hill trolls raised banners (Mike: "Cave trolls nor hill trolls should carry a
/// banner"). Only the race gate is added: Custom Battle keeps its own bearer count, tiers and formation rules.
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
        base.CanAgentBecomeBannerBearer(agent)
            && (!_service.IsEnabled || _service.IsRaceAllowed(agent?.Character?.Race ?? -1));
}
