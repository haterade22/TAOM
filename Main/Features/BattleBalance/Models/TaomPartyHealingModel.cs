using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.BattleBalance.Models;

public class TaomPartyHealingModel : DefaultPartyHealingModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IBattleBalanceConfigProvider _configProvider;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
        IBattleBalanceConfigProvider configProvider,
        ICareerPassiveService careerPassives)
    {
        _settings = settings;
        _configProvider = configProvider;
        _careerPassives = careerPassives;
    }

    public override float GetSurvivalChance(PartyBase party, CharacterObject character,
        DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
    {
        float vanillaSurvival = base.GetSurvivalChance(
            party, character, damageType, canDamageKillEvenIfBlunt, enemyParty);

        if (party == null)
            return vanillaSurvival;

        float result = vanillaSurvival;

        if (_settings.EnableCulturalSurvivalBonuses)
        {
            var config = _configProvider.GetConfig();
            if (config.CasualtyRatios.EnableCulturalSurvivalBonuses)
            {
                // Vanilla PartyBaseHelper.HasFeat precedence — same fix family Codex 43 + 46
                // applied to feat-keyed models. Per-culture survival bonus is a culture-keyed
                // config lookup; should use the same leader→party→owner→settlement walk.
                var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
                if (culture != null)
                {
                    float bonus = config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId);
                    if (bonus != 0f)
                        result = ApplyCulturalSurvivalBonus(result, bonus);
                }
            }
        }

        // Career passive: TroopRegeneration increases survival chance. GetSurvivalChance returns a
        // raw float (not ExplainedNumber), so the multiplicative apply stays inline here.
        var hero = party.Owner ?? party.LeaderHero;
        if (hero != null)
        {
            float magnitude = _careerPassives.GetPassiveMagnitude(hero.StringId, PassiveEffectType.TroopRegeneration);
            if (magnitude != 0f)
                result = Math.Min(1f, result * (1f + magnitude));
        }

        return result;
    }

    public override ExplainedNumber GetDailyHealingHpForHeroes(PartyBase party, bool isPrisoners, bool includeDescriptions = false)
    {
        var result = base.GetDailyHealingHpForHeroes(party, isPrisoners, includeDescriptions);
        // Career passive: HealthRegeneration boosts the party's hero daily HP recovery. Owner ??
        // LeaderHero matches the sibling GetSurvivalChance resolution above.
        _careerPassives.ApplyFactor((party?.Owner ?? party?.LeaderHero)?.StringId, ref result, PassiveEffectType.HealthRegeneration);
        return result;
    }

    internal static float ApplyCulturalSurvivalBonus(float vanillaSurvival, float culturalBonus)
    {
        if (culturalBonus == 0f)
            return vanillaSurvival;

        float deathChance = 1f - vanillaSurvival;
        float newDeathChance = deathChance * (1f - culturalBonus);
        float result = 1f - newDeathChance;
        return Math.Max(0f, Math.Min(1f, result));
    }
}
