namespace TAOM.Features.BattleBalance;

public interface IBattleBalanceSettingsProvider
{
    bool EnableCustomTroopPower { get; }
    bool OverrideVanillaTierPower { get; }
    float Tier7Power { get; }
    float Tier8Power { get; }
    float Tier9Power { get; }
    float Tier10Power { get; }
    float HeroMultiplier { get; }
    float MountedMultiplier { get; }

    bool EnableCustomCasualtyRatios { get; }
    float PlayerBluntDamageChance { get; }
    float AIBluntDamageChance { get; }
    bool EnableCulturalSurvivalBonuses { get; }
}
