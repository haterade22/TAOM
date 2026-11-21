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

    /// <summary>
    /// True for the length of a command trial run that names a stamina step (#1506): the four getters below then read on, so a saved
    /// MCM value cannot fail a step for a reason that has nothing to do with stamina. Set at <c>CommandTrialRun.Arm</c>, cleared at <c>Finish</c>.
    /// </summary>
    internal static bool TrialOn { get; set; }

    /// <summary>Off without MCM, as the compiled default is.</summary>
    public bool Enabled => TrialOn || (Settings?.EnableStamina ?? false);

    /// <summary>
    /// On every cost, for every soldier. The slider is 0.5 to 2; a value outside it, or not a number (a settings file
    /// edited by hand, the dedicated server's JSON), reads as 1.
    /// </summary>
    public float CostScale
    {
        get
        {
            if (TrialOn)
                return 1f;

            var scale = Settings?.StaminaCostScale ?? 1f;
            return FiniteFloatValidator.IsFiniteInRange(scale, MinScale, MaxScale) ? scale : 1f;
        }
    }

    /// <summary>The soldier the player controls has stamina. Off exempts him.</summary>
    public bool ApplyToPlayer => TrialOn || (Settings?.StaminaApplyToPlayer ?? true);

    /// <summary>The soldiers the AI controls have stamina. Off exempts them all.</summary>
    public bool ApplyToAi => TrialOn || (Settings?.StaminaApplyToAi ?? true);
}
