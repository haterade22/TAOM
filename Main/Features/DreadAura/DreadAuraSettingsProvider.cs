using TAOM.Core.Validation;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Features.DreadAura;

/// <summary>
/// Merges MCM live values over the validated JSON defaults (CombatMechanicsSettingsProvider
/// pattern). <c>TaomSettings.Instance</c> can be null very early in startup or when MCM fails to
/// load, so every read falls back to JSON.
/// </summary>
public sealed class DreadAuraSettingsProvider : IDreadAuraSettingsProvider
{
    // The ceilings from DreadAuraConfigProvider, enforced HERE as well because these values
    // have two entry points. MCM's slider metadata is UI-only — MCM's own JSON deserializer assigns
    // without a range check, so a stale or hand-edited settings file bypasses the slider bounds
    // entirely. CombatMechanics shipped exactly this drift on 2026-07-02: the JSON side enforced an
    // invariant the slider did not.
    //
    // These mirror the SLIDER bounds, not the JSON ceilings, because that is the invariant this
    // class exists to re-enforce: an MCM value may not exceed what the slider could have produced.
    // The JSON validator accepts more (radius down to 0.1, rate up to 50), but SettingClamp clamps
    // the JSON fallback to these bounds as well, so no value outside them ever reaches a pulse.
    private const float MinRadius = 4f;

    // Above 15 m native IMBMission.GetNearbyAgentsAux stops searching its proximity grid and walks
    // every agent in the mission (15f is what ProximityMapMaxSearchRadius returns on v1.5.5).
    private const float MaxRadius = 15f;
    private const float MaxMoralePerSecond = 20f;

    private readonly DreadAuraConfig _defaults;

    // Read every frame by DreadAuraMissionLogic (through DreadAuraService.IsEnabled): cached on the first
    // non-null read, read through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public DreadAuraSettingsProvider(IDreadAuraConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    internal DreadAuraSettingsProvider(IDreadAuraConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;

    public bool IsEnabled => Settings?.EnableDreadAura ?? _defaults.Enabled;

    public float Radius
        => SettingClamp.Clamp(Settings?.DreadAuraCloseRadius, DefaultRadius, MinRadius, MaxRadius);

    // JSON only — no MCM knob, so no clamp beyond what the config provider already validated
    // (including the innerRadius <= radius ordering invariant).
    public float InnerRadius => _defaults.Profile?.InnerRadius ?? new DreadProfileConfig().InnerRadius;

    public float MoralePerSecond
        => SettingClamp.Clamp(
            Settings?.DreadAuraMoralePerSecond, DefaultMoralePerSecond, 0f, MaxMoralePerSecond);

    public bool AffectsPlayerTroops => Settings?.DreadAuraAffectsPlayerTroops ?? true;

    private float DefaultRadius => _defaults.Profile?.Radius ?? new DreadProfileConfig().Radius;

    private float DefaultMoralePerSecond
        => _defaults.Profile?.MoralePerSecond ?? new DreadProfileConfig().MoralePerSecond;
}
