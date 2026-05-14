using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TAOM.Features.CulturalFeats;

/// <summary>
/// Boundary adapter around <see cref="TaleWorlds.Core.BasicCultureObject"/>
/// for feat-presence queries. Lets <see cref="ICulturalFeatsService"/>
/// stay free of sealed TaleWorlds culture types so it can be unit-tested
/// with NSubstitute (ADR-007). <c>null</c> adapter inputs to the service
/// represent "no owning culture" (e.g. a wandering caravan) and must be
/// treated as "has no feats".
/// </summary>
public interface ICultureFeatAdapter
{
    /// <summary>
    /// True when the wrapped culture declares <paramref name="feat"/> as one of
    /// its cultural feats. Returns false for a null-feat input (defensive — the
    /// feat registry hands out nulls before <c>CreateAndRegister</c> runs).
    /// </summary>
    bool HasFeat(FeatObject feat);
}
