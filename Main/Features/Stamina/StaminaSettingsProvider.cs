using TAOM.Core.Validation;

namespace TAOM.Features.Stamina;

// The MCM values (Battle Balance/Stamina in TaomSettings). The switch is read when a battle's gate is decided; the
// cost scale is read once per mission tick, never per soldier. The settings object is taken on the first non-null
// read and read through (RaceAbilitySettingsProvider pattern); HotPathSettingsProvidersTests holds it to that.
public class StaminaSettingsProvider
{
    private const float MinScale = 0.5f;
    private const float MaxScale = 2f;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public StaminaSettingsProvider() { }
    internal StaminaSettingsProvider(TaomSettings settings) => _settings = settings;

    /// <summary>Off without MCM, as the compiled default is.</summary>
    public bool Enabled => Settings?.EnableStamina ?? false;

    /// <summary>
    /// On every cost, for every soldier. The slider is 0.5 to 2; a value outside it, or not a number (a settings file
    /// edited by hand, the dedicated server's JSON), reads as 1.
    /// </summary>
    public float CostScale
    {
        get
        {
            var scale = Settings?.StaminaCostScale ?? 1f;
            return FiniteFloatValidator.IsFiniteInRange(scale, MinScale, MaxScale) ? scale : 1f;
        }
    }

    /// <summary>The soldier the player controls has stamina. Off exempts him.</summary>
    public bool ApplyToPlayer => Settings?.StaminaApplyToPlayer ?? true;

    /// <summary>The soldiers the AI controls have stamina. Off exempts them all.</summary>
    public bool ApplyToAi => Settings?.StaminaApplyToAi ?? true;
}
