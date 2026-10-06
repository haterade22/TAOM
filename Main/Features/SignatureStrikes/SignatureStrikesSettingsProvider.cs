using TAOM.Core.Validation;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Merges MCM live values over the validated JSON defaults (CombatMechanicsSettingsProvider
/// pattern). The multiplier is a separate factor on the JSON cooldowns, not a second copy of them,
/// so the only invariant the two surfaces share is "finite and positive", enforced here because
/// MCM's own deserializer assigns a hand-edited settings file without a range check.
/// </summary>
public sealed class SignatureStrikesSettingsProvider : ISignatureStrikesSettingsProvider
{
    private const float MinMultiplier = 0.5f;
    private const float MaxMultiplier = 5f;

    private readonly SignatureStrikesConfig _defaults;

    // Read on every melee hit a signature hero lands, so the settings object is taken once, on the first non-null
    // read, and read through (BattleBalanceSettingsProvider pattern; HotPathSettingsProvidersTests, #746).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public SignatureStrikesSettingsProvider(ISignatureStrikesConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled
    {
        get
        {
            var settings = Settings;
            return (settings?.EnableCombatMechanics ?? true) && (settings?.EnableSignatureStrikes ?? _defaults.Enabled);
        }
    }

    public float CooldownMultiplier
        => SettingClamp.Clamp(Settings?.SignatureStrikeCooldownMultiplier, 1f, MinMultiplier, MaxMultiplier);
}
