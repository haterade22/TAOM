using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Core.Domain;

/// <summary>
/// Cross-feature seam for spotting-range bonuses on <c>TaomMapVisibilityModel</c>.
///
/// <para>The engine holds ONE MapVisibilityModel slot and CareerSystem's model owns it
/// (SubModule.cs registration). Any other feature wanting to widen a party's sight (FieldCamp's
/// lookout) contributes through this interface instead of registering a second model, which would
/// silently unseat the first (the collision docs/roadmap.md used to invite).</para>
/// </summary>
public interface IPartySpottingContributor
{
    /// <summary>
    /// Additional spotting-range factor for this party, e.g. 0.25 for +25%. Return 0 for none.
    /// Called from the model's hot path: no allocation, no IoC, null-tolerant on the party.
    /// </summary>
    float GetSpottingRangeBonusFactor(MobileParty party);
}
