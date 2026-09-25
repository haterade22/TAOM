namespace TAOM.Features.BattleBalance;

public class BattleBalanceSettingsProvider : IBattleBalanceSettingsProvider
{
    // HOT PATH NOTE: GetDefaultTroopPower reads up to 7 of these members per call, and the engine
    // calls it once per casualty, twice per XP-scored hit (DefaultCombatXpModel.GetXpFromHit, live
    // and simulated battles) and once per roster row in every party strength sum. Resolving
    // TaomSettings.Instance per read (two ConcurrentDictionary lookups plus MCM's settings-container
    // search) was the cost (PERF-04). The reference is cached on the first non-null read and read
    // THROUGH, never snapshotted: MCM edits its one registered instance in place, so live MCM edits
    // still apply. Cached lazily, not in the constructor, so a resolve before MCM's
    // OnBeforeInitialModuleScreenSetAsRoot cannot pin the defaults for the session (the
    // NameplateRelationSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public BattleBalanceSettingsProvider() { }
    internal BattleBalanceSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableCustomTroopPower      => Settings?.EnableCustomTroopPower      ?? true;
    public bool OverrideVanillaTierPower    => Settings?.OverrideVanillaTierPower    ?? false;
    public float Tier7Power                 => Settings?.Tier7Power                  ?? 2.91f;
    public float Tier8Power                 => Settings?.Tier8Power                  ?? 3.26f;
    public float Tier9Power                 => Settings?.Tier9Power                  ?? 3.61f;
    public float Tier10Power                => Settings?.Tier10Power                 ?? 3.96f;
    public float HeroMultiplier             => Settings?.HeroMultiplier              ?? 1.5f;
    public float MountedMultiplier          => Settings?.MountedMultiplier           ?? 1.2f;

    public bool EnableCustomCasualtyRatios  => Settings?.EnableCustomCasualtyRatios  ?? true;
    public float PlayerBluntDamageChance    => Settings?.PlayerBluntDamageChance     ?? 0.30f;
    public float AIBluntDamageChance        => Settings?.AIBluntDamageChance         ?? 0.10f;
    public bool EnableCulturalSurvivalBonuses => Settings?.EnableCulturalSurvivalBonuses ?? true;
}
