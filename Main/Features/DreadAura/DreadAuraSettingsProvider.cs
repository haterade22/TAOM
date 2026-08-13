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
    // The design ceilings from DreadAuraConfigProvider, enforced HERE as well because these values
    // have two entry points. MCM's slider metadata is UI-only — MCM's own JSON deserializer assigns
    // without a range check, so a stale or hand-edited settings file bypasses the slider bounds
    // entirely. CombatMechanics shipped exactly this drift on 2026-07-02: the JSON side enforced an
    // invariant the slider did not.
    //
    // These mirror the SLIDER bounds, not the JSON ceilings, because that is the invariant this
    // class exists to re-enforce. The JSON side may legitimately go higher (radius 30, rate 50);
    // an MCM value may not exceed what the slider could have produced.
    private const float MinRadius = 4f;
    private const float MaxRadius = 30f;
    private const float MaxMoralePerSecond = 20f;

    private readonly DreadAuraConfig _defaults;

    public DreadAuraSettingsProvider(IDreadAuraConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableDreadAura ?? _defaults.Enabled;

    public float Radius
        => SettingClamp.Clamp(TaomSettings.Instance?.DreadAuraRadius, DefaultRadius, MinRadius, MaxRadius);

    // JSON only — no MCM knob, so no clamp beyond what the config provider already validated
    // (including the innerRadius <= radius ordering invariant).
    public float InnerRadius => _defaults.Profile?.InnerRadius ?? new DreadProfileConfig().InnerRadius;

    public float MoralePerSecond
        => SettingClamp.Clamp(
            TaomSettings.Instance?.DreadAuraMoralePerSecond, DefaultMoralePerSecond, 0f, MaxMoralePerSecond);

    public bool AffectsPlayerTroops => TaomSettings.Instance?.DreadAuraAffectsPlayerTroops ?? true;

    private float DefaultRadius => _defaults.Profile?.Radius ?? new DreadProfileConfig().Radius;

    private float DefaultMoralePerSecond
        => _defaults.Profile?.MoralePerSecond ?? new DreadProfileConfig().MoralePerSecond;
}
