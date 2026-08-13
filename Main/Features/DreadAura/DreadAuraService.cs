using System;
using TAOM.Core.Validation;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Features.DreadAura;

/// <summary>
/// The pure decision layer for the Aura of Unnatural Dread.
///
/// Every gate below is written as a POSITIVE requirement to proceed rather than an inverted
/// early-exit, because IEEE-754 makes every NaN comparison false: <c>if (rate &lt;= 0) return</c>
/// would let a NaN rate through, and <c>if (distance &gt; radius) continue</c> with a NaN radius
/// would admit every agent in the mission (csharp-architecture.md, six shipped instances).
/// </summary>
public sealed class DreadAuraService : IDreadAuraService
{
    private readonly DreadAuraConfig _config;
    private readonly IDreadAuraSettingsProvider _settings;
    private readonly IDreadRegistry _registry;

    public DreadAuraService(
        IDreadAuraConfigProvider configProvider,
        IDreadAuraSettingsProvider settings,
        IDreadRegistry registry)
    {
        // The provider caches behind a Lazy, so holding the reference just avoids an interface
        // call per target per pulse. Values are read live off the object, not snapshotted.
        _config = configProvider.GetConfig();
        _settings = settings;
        _registry = registry;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public float PulseIntervalSeconds => _config.PulseIntervalSeconds;

    public int MaxSourcesPerFrame => _config.MaxSourcesPerFrame;

    public float ComputeDrain(in DreadDrainContext context)
    {
        if (!_settings.IsEnabled)
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.ResistScaledRate) || !(context.ResistScaledRate > 0f))
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.DistanceSq) || !(context.DistanceSq >= 0f))
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.Radius) || !(context.Radius > 0f))
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.InnerRadius) || !(context.InnerRadius >= 0f))
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.ElapsedSeconds) || !(context.ElapsedSeconds > 0f))
            return 0f;

        if (!FiniteFloatValidator.IsFinite(context.CurrentMorale))
            return 0f;

        var floor = _config.Profile?.MoraleFloor ?? 0f;
        if (!FiniteFloatValidator.IsFinite(floor))
            return 0f;

        // Headroom above the floor. This also rejects the engine's -1f "no CommonAIComponent"
        // sentinel (the player and every non-AI agent), which must FAIL the gate rather than pass
        // it as "already drained".
        var headroom = context.CurrentMorale - floor;
        if (!(headroom > 0f))
            return 0f;

        if (!(context.DistanceSq <= context.Radius * context.Radius))
            return 0f;

        var falloff = ComputeFalloff(in context);
        if (!(falloff > 0f))
            return 0f;

        var resist = _registry.ResolveResist(context.TargetRaceId);
        if (!FiniteFloatValidator.IsFinite(resist) || !(resist > 0f))
            return 0f;

        var drain = context.ResistScaledRate * falloff * resist * context.ElapsedSeconds;
        if (!FiniteFloatValidator.IsFinite(drain) || !(drain > 0f))
            return 0f;

        // Clamped to the headroom so a caller that applies the result blindly still cannot push a
        // target past the configured floor, however large the elapsed time or the rate.
        return drain < headroom ? drain : headroom;
    }

    public bool ShouldPlayFearVoice(float roll)
    {
        if (!_settings.IsEnabled)
            return false;

        var chance = _config.FearVoiceChancePerPulse;
        if (!FiniteFloatValidator.IsFinite(chance) || !(chance > 0f))
            return false;

        return FiniteFloatValidator.IsFinite(roll) && roll < chance;
    }

    /// <summary>1.0 inside the inner radius, falling linearly to 0 at the outer radius.
    /// An inner radius at or above the outer one means a flat bubble, not a division by a
    /// negative band width: the MCM radius slider can legitimately sit below the JSON inner
    /// radius, and that must degrade to "full rate inside the smaller circle".</summary>
    private static float ComputeFalloff(in DreadDrainContext context)
    {
        if (!(context.InnerRadius < context.Radius))
            return 1f;

        var distance = (float)Math.Sqrt(context.DistanceSq);
        if (!(distance > context.InnerRadius))
            return 1f;

        return (context.Radius - distance) / (context.Radius - context.InnerRadius);
    }
}
