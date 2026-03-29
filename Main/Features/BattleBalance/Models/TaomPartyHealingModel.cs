using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.BattleBalance.Models;

public class TaomPartyHealingModel : DefaultPartyHealingModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IBattleBalanceConfigProvider _configProvider;

    public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
        IBattleBalanceConfigProvider configProvider)
    {
        _settings = settings;
        _configProvider = configProvider;
    }

    public override float GetSurvivalChance(PartyBase party, CharacterObject character,
        DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
    {
        float vanillaSurvival = base.GetSurvivalChance(
            party, character, damageType, canDamageKillEvenIfBlunt, enemyParty);

        if (!_settings.EnableCulturalSurvivalBonuses)
            return vanillaSurvival;

        var config = _configProvider.GetConfig();
        if (!config.CasualtyRatios.EnableCulturalSurvivalBonuses)
            return vanillaSurvival;

        var culture = party.Owner?.Culture ?? party.Culture;
        if (culture == null)
            return vanillaSurvival;

        float bonus = config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId);
        if (bonus == 0f)
            return vanillaSurvival;

        return ApplyCulturalSurvivalBonus(vanillaSurvival, bonus);
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
