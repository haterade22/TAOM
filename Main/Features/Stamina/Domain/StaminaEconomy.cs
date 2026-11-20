namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// The numbers of the stamina economy: how large a pool is, what each action costs, how running drains and how
/// stamina comes back (#1497). The values here are the compiled defaults; StaminaConfigProvider replaces each with the
/// validated value of stamina.json before the object is published, and nothing writes it after that, so any thread
/// may read it. Costs and the pool are in stamina points; the run, regeneration and band values are fractions of a
/// soldier's own pool, so they mean the same for a recruit and a hero.
/// </summary>
public sealed class StaminaEconomy
{
    public static readonly StaminaEconomy Default = new StaminaEconomy();

    // Pool: base + perTier x battle tier (0 to 7, every hero 7); a hero adds the hero bonus and perLevel x his level,
    // the level counted up to the cap, so a seasoned lord outlasts a new companion.
    public float PoolBase { get; internal set; } = 100f;
    public float PoolPerTier { get; internal set; } = 10f;
    public float HeroBonus { get; internal set; } = 30f;
    public float HeroPerLevel { get; internal set; } = 1f;
    public float HeroLevelCap { get; internal set; } = 40f;

    // A melee swing or thrust, hit or miss, paid at release: clamp(base + perKg x weapon kg, min, max).
    public float SwingBase { get; internal set; } = 4f;
    public float SwingPerKg { get; internal set; } = 2f;
    public float SwingMin { get; internal set; } = 5f;
    public float SwingMax { get; internal set; } = 14f;

    public float BowShot { get; internal set; } = 4f;
    public float CrossbowShot { get; internal set; } = 2f;
    public float Throw { get; internal set; } = 5f;
    public float Jump { get; internal set; } = 8f;
    public float Kick { get; internal set; } = 6f;
    public float Bash { get; internal set; } = 6f;

    // A drawn bow held longer than the grace time, per second. Player-controlled soldiers only.
    public float BowHoldPerSecond { get; internal set; } = 2f;
    public float BowHoldGraceSeconds { get; internal set; } = 1.5f;

    // The defender's cost of a stopped blow. A shield pays for the damage the shield took; a chamber block is free.
    public float Parry { get; internal set; } = 6f;
    public float ShieldBase { get; internal set; } = 3f;
    public float ShieldPerDamage { get; internal set; } = 0.05f;
    public float ShieldMax { get; internal set; } = 10f;
    public float MissileBlock { get; internal set; } = 1f;

    // Running stops regeneration and drains this share of the pool per second, but never below the floor.
    public float RunDrainPerSecond { get; internal set; } = 0.02f;
    public float RunFloor { get; internal set; } = 0.5f;
    public float RunSpeedThreshold { get; internal set; } = 3f;

    // Regeneration: a share of the pool per second, paused after the soldier's own action. A block or a hit taken does
    // not pause it: with a pause there, every soldier in contact reaches zero whatever he does (the plan's review).
    public float RegenPerSecond { get; internal set; } = 0.05f;
    public float RegenPauseSeconds { get; internal set; } = 1.5f;
    public float BlockingRegen { get; internal set; } = 0.5f;
    public float ExhaustedRegen { get; internal set; } = 1.5f;

    // Bands, as shares of the pool. Exhausted starts at zero and ends at RecoverAt, into Tired.
    public float WindedBelow { get; internal set; } = 0.6f;
    public float TiredBelow { get; internal set; } = 0.3f;
    public float Hysteresis { get; internal set; } = 0.05f;
    public float RecoverAt { get; internal set; } = 0.3f;
}
