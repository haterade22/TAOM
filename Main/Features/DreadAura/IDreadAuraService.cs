using TAOM.Features.DreadAura.Domain;

namespace TAOM.Features.DreadAura;

/// <summary>
/// The pure decision layer for the Aura of Unnatural Dread. Primitives and value types only, no
/// engine types (ADR-007), so the drain arithmetic is fully unit-testable.
/// </summary>
public interface IDreadAuraService
{
    /// <summary>Master gate, folding the MCM toggle over the validated JSON default.</summary>
    bool IsEnabled { get; }

    /// <summary>Seconds between pulses for a single source.</summary>
    float PulseIntervalSeconds { get; }

    /// <summary>Maximum sources allowed to pulse in one frame.</summary>
    int MaxSourcesPerFrame { get; }

    /// <summary>
    /// Morale to subtract from this target for this pulse, always &gt;= 0. Returns 0 for every skip
    /// case: disabled, out of range, non-finite input, already at the floor, fully resistant.
    ///
    /// Never returns more than the target's headroom above the configured floor, so the caller
    /// cannot overshoot it by applying the result blindly.
    /// </summary>
    float ComputeDrain(in DreadDrainContext context);

    /// <summary>
    /// True if this pulse should make one affected agent cry out. <paramref name="roll"/> is a
    /// caller-supplied random in [0, 1); a non-finite roll returns false rather than passing the
    /// comparison by NaN default.
    /// </summary>
    bool ShouldPlayFearVoice(float roll);
}
