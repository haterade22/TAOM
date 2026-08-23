using TAOM.Core.Domain;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// A raised lookout widens the main party's spotting range: +20% plus the LEADER's Scouting/200
/// (the source module's FieldCampVisibilityModel read <c>party.LeaderHero</c>, kept here; every
/// other camp skill read also uses the leader, so the feature stays internally consistent). The
/// factor rides the contributor seam because the CareerSystem model owns the engine's one
/// MapVisibilityModel slot.
///
/// <para>Gated on the master toggle: a disabled feature must not keep a simulation-relevant sight
/// bonus alive on a leftover camp (round-A toggle-off matrix).</para>
///
/// <para>Hot path: called per spotting query. No allocation, no IoC; one dictionary probe on the
/// camp book via the injected service.</para>
/// </summary>
public sealed class LookoutSpottingContributor : IPartySpottingContributor
{
    private readonly ICampService _camps;
    private readonly ICampSettingsProvider _settings;

    public LookoutSpottingContributor(ICampService camps, ICampSettingsProvider settings)
    {
        _camps = camps;
        _settings = settings;
    }

    public float GetSpottingRangeBonusFactor(MobileParty party)
    {
        if (!_settings.Enabled)
            return 0f;
        if (party == null || !party.IsMainParty)
            return 0f;

        var camp = _camps.PlayerCamp;
        if (camp == null || camp.TypeEnum != Domain.CampType.Lookout || !camp.IsReady)
            return 0f;

        float scouting = party.LeaderHero?.GetSkillValue(TaleWorlds.Core.DefaultSkills.Scouting) ?? 0f;
        return ComputeLookoutBonus(scouting);
    }

    /// <summary>Pure source math (AmbushMath.ComputeLookoutBonus): 0.2 + scouting/200. A corrupt
    /// (negative or non-finite) skill degrades to the flat bonus; no arbitrary upper cutoff.</summary>
    internal static float ComputeLookoutBonus(float scouting)
    {
        float bonus = 0.2f + (scouting > 0f ? scouting / 200f : 0f);
        return TAOM.Core.Validation.FiniteFloatValidator.IsFinite(bonus) ? bonus : 0.2f;
    }
}
