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

    /// <summary>Longest the machine holds riders in a line-up (Forming) or a reform (Reforming)
    /// before it proceeds regardless of alignment. The floor that keeps a hold state from ever
    /// freezing the formation. Seconds, [1..15], default 4.</summary>
    float MaxLineUpSeconds { get; }

    bool IsDebugMode { get; }
}
