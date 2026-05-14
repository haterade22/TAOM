using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TAOM.Features.CulturalFeats;

/// <summary>
/// Production wrapper around a sealed <see cref="CultureObject"/>. Constructed
/// at the boundary in each <c>Taom*Model</c> override; the underlying culture
/// reference is never exposed to the service. <c>HasFeat</c> lives on
/// <see cref="CultureObject"/> (Campaign system), not its
/// <c>BasicCultureObject</c> base in TaleWorlds.Core — keeping the type as
/// <c>CultureObject</c> matches how every model already pulls culture
/// (via <c>party.Owner.Culture</c>, <c>town.OwnerClan.Culture</c>, etc.).
/// </summary>
public sealed class CultureFeatAdapter : ICultureFeatAdapter
{
    private readonly CultureObject _culture;

    public CultureFeatAdapter(CultureObject culture)
    {
        _culture = culture;
    }

    public bool HasFeat(FeatObject feat)
    {
        if (_culture == null || feat == null)
            return false;
        return _culture.HasFeat(feat);
    }

    /// <summary>
    /// Convenience boundary helper: returns null when <paramref name="culture"/>
    /// is null so the model overrides can keep their "no culture → skip"
    /// short-circuit at a single point.
    /// </summary>
    public static ICultureFeatAdapter? FromOrNull(CultureObject? culture)
        => culture == null ? null : new CultureFeatAdapter(culture);
}
