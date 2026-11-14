using TAOM.Core.Validation;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Production wire-up of <see cref="IWarChronicleSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton. The strength is re-validated here because a stale json2 file can hold anything: a
/// non-finite value falls back to 1 and a finite one is clamped into 0 to 2. The army focus multiplier
/// gets the same treatment within 1 to 2.5 (default 1.4). Read on the daily tick (the registry's re-bake,
/// the chronicle's focus push), so the settings object is taken once, on the first non-null read
/// (CampaignHotPathSettingsProvidersTests; BattleBalanceSettingsProvider pattern).
/// </summary>
public class WarChronicleSettingsProvider : IWarChronicleSettingsProvider
{
    internal const float ArmyFocusDefault = 1.4f;
    internal const float ArmyFocusMin = 1f;
    /// <summary>The focus registry's own cap, so the slider, this clamp and the registry cannot drift.</summary>
    internal const float ArmyFocusMax = ArmyTargetFocusRegistry.MaxMultiplier;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public WarChronicleSettingsProvider() { }

    internal WarChronicleSettingsProvider(TaomSettings settings) => _settings = settings;

    public float WarEffectStrength =>
        WarEffectMath.SanitizeStrength(Settings?.WarEffectStrength ?? WarEffectMath.StrengthDefault);

    public bool WarRallyEnabled => Settings?.WarRallyEnabled ?? true;

    public bool ChronicleEnabled => Settings?.WarChronicleEnabled ?? true;

    public bool ChronicleAnnouncements => Settings?.WarChronicleAnnouncements ?? true;

    public bool StoryOffersEnabled => Settings?.WarChronicleStoryMissions ?? true;

    public float ArmyFocusMultiplier =>
        SettingClamp.Clamp(Settings?.WarChronicleArmyFocus, ArmyFocusDefault, ArmyFocusMin, ArmyFocusMax);
}
