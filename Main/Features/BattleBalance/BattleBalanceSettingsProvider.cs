namespace TAOM.Features.BattleBalance;

public class BattleBalanceSettingsProvider : IBattleBalanceSettingsProvider
{
    // HOT PATH NOTE: GetDefaultTroopPower reads up to 7 of these members per troop per
    // simulation round (siege/map-event sim). Resolving TaomSettings.Instance (a
    // ConcurrentDictionary walk) per read was the leak (PERF-04). The reference is cached
    // once in the ctor and read THROUGH (not snapshotted), so live MCM edits still apply —
    // same contract as NameplateFadeSettingsProvider (Patch38). Reuse.Singleton lifetime
    // (BattleBalanceIoC.cs) means one ctor read serves the whole process.
    private readonly TaomSettings _settings;
    public BattleBalanceSettingsProvider() => _settings = TaomSettings.Instance;

    public bool EnableCustomTroopPower      => _settings?.EnableCustomTroopPower      ?? true;
    public bool OverrideVanillaTierPower    => _settings?.OverrideVanillaTierPower    ?? false;
    public float Tier7Power                 => _settings?.Tier7Power                  ?? 2.91f;
    public float Tier8Power                 => _settings?.Tier8Power                  ?? 3.26f;
    public float Tier9Power                 => _settings?.Tier9Power                  ?? 3.61f;
    public float Tier10Power                => _settings?.Tier10Power                 ?? 3.96f;
    public float HeroMultiplier             => _settings?.HeroMultiplier              ?? 1.5f;
    public float MountedMultiplier          => _settings?.MountedMultiplier           ?? 1.2f;

    public bool EnableCustomCasualtyRatios  => _settings?.EnableCustomCasualtyRatios  ?? true;
    public float PlayerBluntDamageChance    => _settings?.PlayerBluntDamageChance     ?? 0.30f;
    public float AIBluntDamageChance        => _settings?.AIBluntDamageChance         ?? 0.10f;
    public bool EnableCulturalSurvivalBonuses => _settings?.EnableCulturalSurvivalBonuses ?? true;
}
