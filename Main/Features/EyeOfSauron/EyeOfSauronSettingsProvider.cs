using TAOM.Core.Validation;
using TAOM.Features.CampaignStartOptions;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled defaults. A stale settings file can hold anything, so every float is
/// checked for NaN and infinity first (a non-finite value reverts to the default) and then clamped into its slider
/// range; ints are clamped (<see cref="SettingClamp"/>). The multiplier is read every campaign frame while a hunt
/// runs, so the settings object is taken once in the lazy accessor and read through
/// (CampaignHotPathSettingsProvidersTests). The constants here are the single source of the sliders' bounds.
/// </summary>
public sealed class EyeOfSauronSettingsProvider : IEyeOfSauronSettings
{
    public const float DefaultThreshold = 10f;
    public const float MinThreshold = 1f;
    public const float MaxThreshold = 100f;
    public const float DefaultHalfLifeDays = 30f;
    public const float MinHalfLifeDays = 1f;
    public const float MaxHalfLifeDays = 365f;
    public const int DefaultCooldownDays = 60;
    public const int MinCooldownDays = 0;
    public const int MaxCooldownDays = 365;
    public const int DefaultHuntTimeoutDays = 20;
    public const int MinHuntTimeoutDays = 1;
    public const int MaxHuntTimeoutDays = 120;
    public const float DefaultSpeedMultiplier = 1.3f;
    public const float MinSpeedMultiplier = 1.1f;
    public const float MaxSpeedMultiplier = 2.0f;
    public const float DefaultStrengthRatio = 1.3f;
    public const float MinStrengthRatio = 0.5f;
    public const float MaxStrengthRatio = 3.0f;
    public const int DefaultMaxTroops = 250;
    public const int MinMaxTroops = 20;
    public const int MaxMaxTroops = 1000;
    public const float DefaultStepCap = 15f;
    public const float MinStepCap = 1f;
    public const float MaxStepCap = 50f;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    private readonly ICampaignStartOptions _campaign;

    public EyeOfSauronSettingsProvider(ICampaignStartOptions campaign) => _campaign = campaign;

    internal EyeOfSauronSettingsProvider(TaomSettings settings, ICampaignStartOptions campaign)
        : this(campaign) => _settings = settings;

    public bool Enabled => _campaign.EyeOfSauron ?? EnabledNewCampaignDefault;

    public bool EnabledNewCampaignDefault => Settings?.EyeOfSauronEnabled ?? true;

    public float Threshold => SettingClamp.Clamp(Settings?.EyeThreshold, DefaultThreshold, MinThreshold, MaxThreshold);

    public float HalfLifeDays =>
        SettingClamp.Clamp(Settings?.EyeHalfLifeDays, DefaultHalfLifeDays, MinHalfLifeDays, MaxHalfLifeDays);

    public int CooldownDays =>
        SettingClamp.Clamp(Settings?.EyeCooldownDays, DefaultCooldownDays, MinCooldownDays, MaxCooldownDays);

    public int HuntTimeoutDays =>
        SettingClamp.Clamp(Settings?.HuntTimeoutDays, DefaultHuntTimeoutDays, MinHuntTimeoutDays, MaxHuntTimeoutDays);

    public float HunterSpeedMultiplier =>
        SettingClamp.Clamp(Settings?.HunterSpeedMultiplier, DefaultSpeedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier);

    public float HunterStrengthRatio =>
        SettingClamp.Clamp(Settings?.HunterStrengthRatio, DefaultStrengthRatio, MinStrengthRatio, MaxStrengthRatio);

    public int HunterMaxTroops => SettingClamp.Clamp(Settings?.HunterMaxTroops, DefaultMaxTroops, MinMaxTroops, MaxMaxTroops);

    public float HunterStepCap => SettingClamp.Clamp(Settings?.HunterStepCap, DefaultStepCap, MinStepCap, MaxStepCap);
}
