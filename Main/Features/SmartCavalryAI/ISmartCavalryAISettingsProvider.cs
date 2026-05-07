namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// Wraps <c>TaomSettings.Instance</c> with default-fallback semantics so the cavalry
/// service can be unit-tested without MCM. Mirrors the pattern from
/// <c>IMixedFormationsSettingsProvider</c>.
/// </summary>
public interface ISmartCavalryAISettingsProvider
{
    bool IsEnabled { get; }
    bool AvoidFriendlies { get; }
    float ChargeFormationStrictness { get; }
    float ReformDistanceAfterCharge { get; }
    float ChargeLineSpacing { get; }
    bool IsDebugMode { get; }
}
