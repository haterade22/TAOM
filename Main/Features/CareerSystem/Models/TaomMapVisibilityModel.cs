using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Domain;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomMapVisibilityModel : DefaultMapVisibilityModel
{
    private readonly ICareerPassiveService _careerPassives;

    // The engine holds ONE MapVisibilityModel slot and this model owns it. Other features widen
    // sight through contributors instead of a second AddModel that would silently unseat this one
    // (FieldCamp's lookout is the first). Array, not IEnumerable: this is a per-party hot path.
    private readonly IPartySpottingContributor[] _contributors;

    public TaomMapVisibilityModel(ICareerPassiveService careerPassives)
        : this(careerPassives, null)
    {
    }

    public TaomMapVisibilityModel(ICareerPassiveService careerPassives, IEnumerable<IPartySpottingContributor> contributors)
    {
        _careerPassives = careerPassives;
        _contributors = contributors == null
            ? System.Array.Empty<IPartySpottingContributor>()
            : System.Linq.Enumerable.ToArray(contributors);
    }

    public override ExplainedNumber GetPartySpottingRange(MobileParty party, bool includeDescriptions = false)
    {
        var result = base.GetPartySpottingRange(party, includeDescriptions);
        _careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySpottingRange);

        for (int i = 0; i < _contributors.Length; i++)
        {
            var factor = _contributors[i].GetSpottingRangeBonusFactor(party);
            // Positive requirement: a NaN or non-positive contribution adds nothing.
            if (factor > 0f && !float.IsInfinity(factor))
                result.AddFactor(factor);
        }

        return result;
    }

    // StealthBonus — how easily OTHERS spot this party. A LOWER ratio means the party must be
    // closer before it is seen, so a positive bonus reduces it (party is harder to detect). The
    // direction-inverting math lives in the testable CareerPassiveMath.ApplyStealthRatio. The
    // inverse direction (how far the party itself sees) is GetPartySpottingRange above.
    public override float GetPartySpottingRatioForMainPartySeeingRange(MobileParty party)
    {
        var ratio = base.GetPartySpottingRatioForMainPartySeeingRange(party);
        var bonus = _careerPassives.GetPassiveMagnitude(party?.LeaderHero?.StringId, PassiveEffectType.StealthBonus);
        return CareerPassiveMath.ApplyStealthRatio(ratio, bonus);
    }
}
