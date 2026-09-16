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

    public SignatureStrikesSettingsProvider(ISignatureStrikesConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    // Read once per call: this runs on every melee hit a signature hero lands.
    public bool IsEnabled
    {
        get
        {
            var settings = TaomSettings.Instance;
            return (settings?.EnableCombatMechanics ?? true) && (settings?.EnableSignatureStrikes ?? _defaults.Enabled);
        }
    }

    public float CooldownMultiplier
        => SettingClamp.Clamp(TaomSettings.Instance?.SignatureStrikeCooldownMultiplier, 1f, MinMultiplier, MaxMultiplier);
}
