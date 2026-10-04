namespace TAOM.Features.RaceAbilities;

// The MCM switches (Battle Tactics/Race Abilities in TaomSettings), read live. Enabled decides each
// mission's gate on its first tick and every tree's decision; switching it off mid-battle stops new
// activations at once while running ones finish. Read on every decision and every mission tick, so the
// settings object is taken once, on the first non-null read, and read through (BattleBalanceSettingsProvider
// pattern; HotPathSettingsProvidersTests).
public class RaceAbilitySettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public RaceAbilitySettingsProvider() { }
    internal RaceAbilitySettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.EnableRaceAbilities ?? true;

    public bool WarCries => Settings?.RaceAbilityWarCries ?? true;

    public bool Messages => Settings?.RaceAbilityMessages ?? true;

    // One log line per wave and per phase change, on top of the 30 s and mission-end reports.
    public bool DebugLog => Settings?.RaceAbilityDebugLog ?? false;

    // The outline on a soldier whose ability is active and the sparks as he fires; switching it off clears the
    // outlines on the next half-second pulse.
    public bool Glow => Settings?.RaceAbilityGlow ?? true;
}
