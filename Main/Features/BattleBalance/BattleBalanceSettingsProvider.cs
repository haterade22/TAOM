namespace TAOM.Features.BattleBalance;

public class BattleBalanceSettingsProvider : IBattleBalanceSettingsProvider
{
    public bool EnableCustomTroopPower      => TaomSettings.Instance?.EnableCustomTroopPower      ?? true;
    public bool OverrideVanillaTierPower    => TaomSettings.Instance?.OverrideVanillaTierPower    ?? false;
    public float Tier7Power                 => TaomSettings.Instance?.Tier7Power                  ?? 2.91f;
    public float Tier8Power                 => TaomSettings.Instance?.Tier8Power                  ?? 3.26f;
    public float Tier9Power                 => TaomSettings.Instance?.Tier9Power                  ?? 3.61f;
    public float Tier10Power                => TaomSettings.Instance?.Tier10Power                 ?? 3.96f;
    public float HeroMultiplier             => TaomSettings.Instance?.HeroMultiplier              ?? 1.5f;
    public float MountedMultiplier          => TaomSettings.Instance?.MountedMultiplier           ?? 1.2f;

    public bool EnableCustomCasualtyRatios  => TaomSettings.Instance?.EnableCustomCasualtyRatios  ?? true;
    public float PlayerBluntDamageChance    => TaomSettings.Instance?.PlayerBluntDamageChance     ?? 0.30f;
    public float AIBluntDamageChance        => TaomSettings.Instance?.AIBluntDamageChance         ?? 0.10f;
    public bool EnableCulturalSurvivalBonuses => TaomSettings.Instance?.EnableCulturalSurvivalBonuses ?? true;
}
