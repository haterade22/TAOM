using System;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.BattleBalance.Hooks;

/// <summary>
/// The healing model's boundary for the survival roll: the cultural survival bonus and the career TroopSurvival passive, on
/// top of vanilla's roll. Engine facts are read here and the arithmetic is <see cref="SurvivalChanceMath"/>. An enlisted player
/// rolls with his own party, as a freelancer does: vanilla reads the Medicine bonus and Cheat Death off that party's
/// <c>MobileParty.EffectiveSurgeon</c>, which is his assigned surgeon while that hero is in his party and otherwise himself as
/// party leader. His own culture bonus and career TroopSurvival passive apply too.
/// </summary>
internal static class SurvivalChanceLine
{
    /// <summary>Applies the cultural bonus, then the career passive, to vanilla's survival chance.</summary>
    public static float Apply(IBattleBalanceSettingsProvider settings, IBattleBalanceConfigProvider configProvider,
        ICareerPassiveService careerPassives, PartyBase party, float vanillaSurvival)
    {
        if (party == null)
            return vanillaSurvival;

        float result = vanillaSurvival;

        if (settings.EnableCulturalSurvivalBonuses)
        {
            var config = configProvider.GetConfig();
            if (config.CasualtyRatios.EnableCulturalSurvivalBonuses)
            {
                // Vanilla PartyBaseHelper.HasFeat precedence: the leader, party, owner, settlement walk.
                var culture = CultureFeatAdapter.ResolvePartyCulture(party);
                if (culture != null)
                    result = SurvivalChanceMath.ApplyCulturalSurvivalBonus(
                        result, config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId));
            }
        }

        var heroId = CareerPassiveHero.ResolveId(party);
        if (heroId == null)
            return result;
        // Career TroopSurvival, capped at certain survival. ScaleByPassive floors the scale at zero, so a magnitude of -1
        // or less never makes the chance negative.
        return Math.Min(1f, careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival));
    }
}
