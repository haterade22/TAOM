using TAOM.Core.Validation;

namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// MCM bridge for <see cref="INameplateRelationSettingsProvider"/>. Percent sliders become
/// fractions here, and every float is finite-and-range checked before it leaves (a hand-edited
/// TAOM.json can hold anything; NaN passes a plain range compare). Out-of-range or non-finite
/// values revert to the compiled default, which equals the slider default in TaomSettings.
///
/// The <c>TaomSettings.Instance</c> reference is cached on first successful read, not in the
/// constructor: this provider is resolved at container build time (OnSubModuleLoad), which can
/// run before MCM has created the instance, and a null cached there would pin the defaults for
/// the whole session. Once cached, each read is one field dereference plus three compares.
/// </summary>
public class NameplateRelationSettingsProvider : INameplateRelationSettingsProvider
{
    public const float DefaultTintStrengthPercent = 100f;
    public const float DefaultNeutralPlateOpacityPercent = 35f;
    public const float DefaultRelationPlateOpacityPercent = 50f;
    public const float MinTintPercent = 0f;
    public const float MinOpacityPercent = 10f;
    public const float MaxPercent = 100f;

    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool ColorsEnabled => Settings?.EnableNameplateRelationColors ?? true;

    public float TintStrength => NormalizePercent(
        Settings?.NameplateRelationTintStrength ?? DefaultTintStrengthPercent,
        DefaultTintStrengthPercent, MinTintPercent, MaxPercent);

    public float NeutralPlateAlpha => NormalizePercent(
        Settings?.NameplateNeutralPlateOpacity ?? DefaultNeutralPlateOpacityPercent,
        DefaultNeutralPlateOpacityPercent, MinOpacityPercent, MaxPercent);

    public float RelationPlateAlpha => NormalizePercent(
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
}
