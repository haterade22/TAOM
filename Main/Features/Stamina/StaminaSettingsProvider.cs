namespace TAOM.Features.Stamina;

// The MCM switch (Battle Balance/Stamina in TaomSettings), read when a battle's gate is decided and by the dev
// console. The settings object is taken on the first non-null read and read through (RaceAbilitySettingsProvider
// pattern).
public class StaminaSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public StaminaSettingsProvider() { }
    internal StaminaSettingsProvider(TaomSettings settings) => _settings = settings;

    /// <summary>Off without MCM, as the compiled default is.</summary>
    public bool Enabled => Settings?.EnableStamina ?? false;
}
