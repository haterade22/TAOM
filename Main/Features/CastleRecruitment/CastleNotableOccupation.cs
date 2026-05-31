namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// TAOM-side enumeration of the notable occupations a castle is populated with. Mapped to the
/// sealed <c>TaleWorlds.CampaignSystem.Occupation</c> at the behavior boundary (ADR-007) so the
/// pure service never references engine types.
///
/// Deliberately excludes <c>RuralNotable</c>: vanilla <c>DefaultVolunteerModel.GetBasicVolunteer</c>
/// dereferences <c>sellerHero.CurrentSettlement.Village.Bound</c> for rural notables, which NREs for
/// a castle notable (a castle's <c>Settlement.Village</c> is null). All four occupations here
/// satisfy <c>DefaultVolunteerModel.CanHaveRecruits</c> (Occupation 17-22) and are castle-safe.
/// </summary>
public enum CastleNotableOccupation
{
    GangLeader,
    Headman,
    Merchant,
    Artisan,
}
