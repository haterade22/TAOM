using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// MCM bridge for <see cref="INameplateRelationSettingsProvider"/>. Percent sliders become
/// fractions here, and every float is finite-and-range checked before it leaves. MCM's slider
/// clamps to the attribute range, but its JSON loader assigns a hand-edited TAOM.json value as
/// is, so NaN or an out-of-range number can sit in the property; such a value reverts to the
/// compiled default (which equals the slider default in TaomSettings) and is reported ONCE per
/// property, never per read, because the reads run every frame.
///
/// The <c>TaomSettings.Instance</c> reference is cached on first successful read, not in the
/// constructor: this provider is resolved at container build time (OnSubModuleLoad), before MCM
/// has created the instance, and a null cached there would pin the defaults for the session.
/// Once cached, each read is one field dereference plus the range check.
/// </summary>
public class NameplateRelationSettingsProvider : INameplateRelationSettingsProvider
{
    public const float DefaultTintStrengthPercent = 100f;
    public const float DefaultNeutralPlateOpacityPercent = 35f;
    public const float DefaultRelationPlateOpacityPercent = 50f;
    public const float MinTintPercent = 0f;
    public const float MinOpacityPercent = 10f;
    public const float MaxPercent = 100f;

    private readonly IModLogger? _logger;
    private TaomSettings? _settings;
    private int _warnedMask;

    public NameplateRelationSettingsProvider(IModLogger? logger)
    {
        _logger = logger;
    }

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool ColorsEnabled => Settings?.EnableNameplateRelationColors ?? true;

    public float TintStrength => Read(0, nameof(TaomSettings.NameplateRelationTintStrength),
        Settings?.NameplateRelationTintStrength ?? DefaultTintStrengthPercent,
        DefaultTintStrengthPercent, MinTintPercent, MaxPercent);

    public float NeutralPlateAlpha => Read(1, nameof(TaomSettings.NameplateNeutralPlateOpacity),
        Settings?.NameplateNeutralPlateOpacity ?? DefaultNeutralPlateOpacityPercent,
        DefaultNeutralPlateOpacityPercent, MinOpacityPercent, MaxPercent);

    public float RelationPlateAlpha => Read(2, nameof(TaomSettings.NameplateRelationPlateOpacity),
        Settings?.NameplateRelationPlateOpacity ?? DefaultRelationPlateOpacityPercent,
        DefaultRelationPlateOpacityPercent, MinOpacityPercent, MaxPercent);

    /// <summary>A slider percentage as a fraction; anything non-finite or outside
    /// [<paramref name="minPercent"/>, <paramref name="maxPercent"/>] becomes the default.</summary>
    public static float NormalizePercent(float rawPercent, float defaultPercent, float minPercent, float maxPercent)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(rawPercent, minPercent, maxPercent))
            return defaultPercent / 100f;

        return rawPercent / 100f;
    }

    /// <summary>
    /// <see cref="NormalizePercent"/> plus the diagnostic the config-validation rule asks for: the
    /// first time a property's value is refused, one warning names the value, the range and the
    /// default in force. <paramref name="slot"/> is the property's bit in the once-only mask; a
    /// race between the parallel-update and main threads can at worst log twice.
    /// </summary>
    internal float Read(int slot, string propertyName, float rawPercent, float defaultPercent, float minPercent, float maxPercent)
    {
        if (FiniteFloatValidator.IsFiniteInRange(rawPercent, minPercent, maxPercent))
            return rawPercent / 100f;

        var bit = 1 << slot;
        if ((_warnedMask & bit) == 0)
        {
            _warnedMask |= bit;
            _logger?.LogWarning($"[SettlementNameplateRelation] MCM setting {propertyName} = {rawPercent} is not a finite number in "
                              + $"[{minPercent}, {maxPercent}] (a hand-edited TAOM.json?); using the default {defaultPercent} until it is corrected.");
        }
        return defaultPercent / 100f;
    }
}
