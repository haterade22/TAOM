namespace TAOM.Features.SettlementNameplateFade;

/// <summary>
/// Map UI / nameplate-fade settings boundary. Decouples the fade service and patch from the
/// concrete <c>TaomSettings</c> singleton (csharp-architecture.md "Constructor injection only").
///
/// Read each frame from the OnParallelUpdate path — implementations must be cheap to call and
/// thread-safe (Bannerlord widget update fires from parallel-update threads).
/// </summary>
public interface INameplateFadeSettingsProvider
{
    /// <summary>Master toggle. When false, <see cref="NameplateFadeService"/> returns 1.0 (vanilla — no fade).</summary>
    bool Enabled { get; }

    /// <summary>Camera distance at which fade BEGINS. Settlements closer than this stay fully opaque.</summary>
    float NearDistance { get; }

    /// <summary>Camera distance at which fade COMPLETES. Settlements farther than this are fully hidden.</summary>
    float FarDistance { get; }
}
