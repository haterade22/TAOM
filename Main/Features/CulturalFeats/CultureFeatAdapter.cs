using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

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

    /// <summary>
    /// Boundary helper — resolves the culture used for cultural-feat checks
    /// with the same precedence as vanilla <c>PartyBaseHelper.HasFeat</c>:
    /// leader hero → party → owner → settlement. Wraps the result in an
    /// adapter, returning null when no culture can be resolved. Shared by
    /// every GameModel that does culture-keyed feat dispatch on a party
    /// (<c>TaomPartySpeedModel</c>, <c>TaomPartySizeModel</c>, …) so the
    /// precedence lives in one place — fixes the consistency gap Codex
    /// review 43 caught and prevents future drift.
    /// </summary>
    public static ICultureFeatAdapter? FromOrNull(PartyBase? party)
        => FromOrNull(ResolvePartyCulture(party));

    /// <summary>
    /// Boundary helper — same vanilla <c>PartyBaseHelper.HasFeat</c> precedence
    /// as <see cref="FromOrNull(PartyBase?)"/> but returns the raw
    /// <see cref="CultureObject"/> for callers that need the engine type for
    /// a non-feat purpose (e.g. <c>culture.StringId</c> lookup against a
    /// per-culture config). Use the adapter overload when you only need
    /// <c>HasFeat</c>; use this when you need a real culture handle. Returns
    /// null when no culture can be resolved.
    /// </summary>
    public static CultureObject? ResolvePartyCulture(PartyBase? party)
    {
        if (party == null)
            return null;
        // party.Culture is `MapFaction.Culture` with no null guard — it NREs when
        // MapFaction is null (faction-less lord party during army siege-start strength
        // calc). Use the null-safe MapFaction?.Culture equivalent; every step is `?.`
        // per .claude/rules/adapters.md (TaleWorlds getters crash before your null check).
        return party.LeaderHero?.Culture
            ?? party.MapFaction?.Culture
            ?? party.Owner?.Culture
            ?? party.Settlement?.Culture;
    }
}
