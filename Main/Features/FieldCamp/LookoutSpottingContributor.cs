using TAOM.Core.Domain;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// A raised lookout widens the main party's spotting range: +20% plus the leader's Scouting/200
/// (the source module's FieldCampVisibilityModel factor, which could not register on TAOM because
/// the CareerSystem model owns the engine's one MapVisibilityModel slot).
///
/// <para>Hot path: called per spotting query. No allocation, no IoC; one dictionary probe on the
/// camp book via the injected service.</para>
/// </summary>
public sealed class LookoutSpottingContributor : IPartySpottingContributor
{
    private readonly ICampService _camps;

    public LookoutSpottingContributor(ICampService camps)
    {
        _camps = camps;
    }

    public float GetSpottingRangeBonusFactor(MobileParty party)
    {
        if (party == null || !party.IsMainParty)
            return 0f;

        var camp = _camps.PlayerCamp;
        if (camp == null || camp.TypeEnum != Domain.CampType.Lookout || !camp.IsReady)
            return 0f;

        float scouting = party.EffectiveScout?.GetSkillValue(TaleWorlds.Core.DefaultSkills.Scouting) ?? 0f;
        // Positive requirement so a corrupt skill value contributes the flat bonus only.
        var skillBonus = scouting > 0f && scouting < 1000f ? scouting / 200f : 0f;
        return 0.2f + skillBonus;
    }
}
