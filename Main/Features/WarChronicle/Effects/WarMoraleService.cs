using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// The War Chronicle's party morale step for <c>TaomPartyMoraleModel</c>: the kingdom's
/// <see cref="WarEffectKind.PartyMorale"/> multiplier minus one, times vanilla's standard base morale, so a
/// +0.10 effect is +5 points. A party of a player's clan gets nothing (rule D4, as
/// <see cref="TroopProgression.VolunteerProductionService"/>), nor does a party with no kingdom, no effect, or a
/// value that is not finite.
/// </summary>
public sealed class WarMoraleService
{
    private readonly IWarEffectService _war;
    private TextObject? _label;

    public WarMoraleService(IWarEffectService war) => _war = war;

    /// <param name="kingdomId">The party's clan kingdom id; null for none.</param>
    /// <param name="partyOfAPlayersClan">The party belongs to a human player's clan: no war morale.</param>
    /// <param name="standardBaseMorale">Vanilla's <c>GetStandardBaseMorale</c> for the party.</param>
    public float Bonus(string? kingdomId, bool partyOfAPlayersClan, float standardBaseMorale)
    {
        if (partyOfAPlayersClan || kingdomId == null || !FiniteFloatValidator.IsFinite(standardBaseMorale))
            return 0f;

        var multiplier = _war.GetMultiplier(kingdomId, WarEffectKind.PartyMorale);
        if (!FiniteFloatValidator.IsFinite(multiplier) || multiplier == 1f)
            return 0f;

        var bonus = (multiplier - 1f) * standardBaseMorale;
        return FiniteFloatValidator.IsFinite(bonus) ? bonus : 0f;
    }

    /// <summary>
    /// The kingdom has a morale effect now. The morale model asks this first, because it runs for every party on the engine's worker
    /// threads (<c>DefaultPartySpeedCalculatingModel</c> reads <c>MobileParty.Morale</c>) and most campaigns have no such effect.
    /// </summary>
    public bool IsActiveFor(string? kingdomId) => kingdomId != null && _war.GetMultiplier(kingdomId, WarEffectKind.PartyMorale) != 1f;

    public void Apply(string? kingdomId, bool partyOfAPlayersClan, float standardBaseMorale, ref ExplainedNumber result)
    {
        var bonus = Bonus(kingdomId, partyOfAPlayersClan, standardBaseMorale);
        if (bonus != 0f)
            result.Add(bonus, _label ?? (_label = new TextObject("{=taom_war_morale}Tidings of the war")));
    }
}
