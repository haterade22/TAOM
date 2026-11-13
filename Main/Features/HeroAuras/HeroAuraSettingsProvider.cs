namespace TAOM.Features.HeroAuras;

// The MCM switches (Battle Tactics/Hero Auras in TaomSettings), read live on every pulse. The settings object is taken
// on the first non-null read and read through (RaceAbilitySettingsProvider pattern).
public class HeroAuraSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public HeroAuraSettingsProvider() { }
    internal HeroAuraSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => (Settings?.EnableHeroAuras ?? true) && Strength > 0f;

    public float Strength => Settings?.HeroAuraStrength ?? 1f;
}
