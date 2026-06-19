using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Hooks;

public class RecruitmentResourceGateHook : IOnRecruitmentResourceGate
{
    private readonly ISpecialResourceService _service;

    public RecruitmentResourceGateHook(ISpecialResourceService service)
    {
        _service = service;
    }

    public RecruitGateResult EvaluateCart(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart)
        => _service.CanAffordRecruit(heroId, kingdomId, cultureId, cart);
}
