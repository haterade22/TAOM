using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Production wire-up of <see cref="IWarChronicleSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton. The strength is re-validated here because a stale json2 file can hold anything: a
/// non-finite value falls back to 1 and a finite one is clamped into 0 to 2. Read on the daily tick
/// (the registry's re-bake), so the settings object is taken once, on the first non-null read
/// (CampaignHotPathSettingsProvidersTests; BattleBalanceSettingsProvider pattern).
/// </summary>
public class WarChronicleSettingsProvider : IWarChronicleSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public WarChronicleSettingsProvider() { }

    internal WarChronicleSettingsProvider(TaomSettings settings) => _settings = settings;

    public float WarEffectStrength =>
        WarEffectMath.SanitizeStrength(Settings?.WarEffectStrength ?? WarEffectMath.StrengthDefault);

    public bool WarRallyEnabled => Settings?.WarRallyEnabled ?? true;
}
