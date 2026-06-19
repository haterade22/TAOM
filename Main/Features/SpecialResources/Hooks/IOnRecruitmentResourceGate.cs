using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Hooks;

/// <summary>
/// Boundary for the recruit-volunteers screen gate: given the player and the screen's cart,
/// decide whether the Done button should be blocked because the player can't afford the special-resource
/// recruit cost (elephant/spider). Wraps <see cref="ISpecialResourceService.CanAffordRecruit"/>.
/// </summary>
public interface IOnRecruitmentResourceGate
{
    RecruitGateResult EvaluateCart(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart);
}
