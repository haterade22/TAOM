using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.BattleBalance.Models;

public class TaomMilitaryPowerModel : DefaultMilitaryPowerModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IBattleBalanceConfigProvider _configProvider;

    public TaomMilitaryPowerModel(IBattleBalanceSettingsProvider settings,
        IBattleBalanceConfigProvider configProvider)
    {
        _settings = settings;
        _configProvider = configProvider;
    }

    public override float GetDefaultTroopPower(CharacterObject troop)
    {
        if (!_settings.EnableCustomTroopPower)
            return base.GetDefaultTroopPower(troop);

        var config = _configProvider.GetConfig();
        int tier = troop.IsHero ? troop.Level / 4 + 1 : troop.Tier;

        float basePower = CalculateTierPower(tier, _settings.OverrideVanillaTierPower,
            _settings.Tier7Power, _settings.Tier8Power, _settings.Tier9Power, _settings.Tier10Power,
            config.TroopPower);

        float multiplier = troop.IsHero
            ? _settings.HeroMultiplier
            : (troop.IsMounted ? _settings.MountedMultiplier : 1.0f);

        return basePower * multiplier;
    }

    internal static float CalculateTierPower(int tier, bool overrideVanilla,
        float tier7, float tier8, float tier9, float tier10,
        BattleBalanceConfig.TroopPowerSection config)
    {
        if (tier >= 7)
            return tier switch
            {
                7  => tier7,
                8  => tier8,
                9  => tier9,
                10 => tier10,
                _  => config.GetTierPower(tier)
            };

        return overrideVanilla
            ? config.GetTierPower(tier)
            : (2f + tier) * (10f + tier) * 0.02f;
    }
}
