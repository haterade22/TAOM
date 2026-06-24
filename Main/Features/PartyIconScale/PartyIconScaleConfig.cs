using TAOM.Core.Validation;

namespace TAOM.Features.PartyIconScale;

/// <summary>
/// Resolves the campaign-map party-icon figure/mount scale from the MCM "Map Figure Scale" slider
/// (<see cref="TaomSettings.MapFigureScale"/>).
/// <para>
/// <see cref="GetScale"/> is the static that the <c>Patch53_PartyIconScale</c> transpiler rewrites the
/// hardcoded vanilla <c>0.3f</c> literal in <c>MobilePartyVisual.AddCharacterToPartyIcon</c> into a
/// <c>call</c> of. It MUST stay a public, parameterless, <see cref="float"/>-returning static so the IL
/// <c>call</c> stays stack-neutral with the <c>ldc.r4</c> it replaces. <see cref="Resolve"/> holds the
/// pure, validated logic (tested via <c>InternalsVisibleTo("TAOM.Tests")</c>).
/// </para>
/// <para>
/// Validation falls back to <see cref="Default"/> for NaN / ±Infinity / out-of-range / null per the
/// "Config Providers MUST Validate" rule. It deliberately does NOT log on fallback: the MCM slider already
/// clamps user input to [<see cref="Min"/>, <see cref="Max"/>], so an invalid value is only reachable via a
/// hand-corrupted settings JSON, and <see cref="GetScale"/> runs on every party-icon rebuild — per-call
/// warning logging would spam the log for a value the UI already guards.
/// </para>
/// </summary>
public static class PartyIconScaleConfig
{
    /// <summary>Default map-figure scale — half of vanilla's 0.3.</summary>
    public const float Default = 0.15f;

    /// <summary>Smallest allowed scale; below this figures are effectively invisible on the map.</summary>
    public const float Min = 0.05f;

    /// <summary>Largest allowed scale; 1.0 lets a user make figures larger than vanilla (0.3) for testing.</summary>
    public const float Max = 1.0f;

    /// <summary>
    /// Validates a raw slider value. Returns it when finite and within [<see cref="Min"/>, <see cref="Max"/>];
    /// otherwise (NaN / ±Infinity / out-of-range / null) returns <see cref="Default"/>.
    /// </summary>
    internal static float Resolve(float? raw) =>
        raw is float v && FiniteFloatValidator.IsFiniteInRange(v, Min, Max) ? v : Default;

    /// <summary>
    /// The map-figure scale the engine's party-icon builder uses. Invoked from the rewritten IL of
    /// <c>MobilePartyVisual.AddCharacterToPartyIcon</c> (people site directly, mount site as
    /// <c>ScaleFactor * GetScale()</c>). Reads the live MCM value so a slider change applies on the next
    /// icon rebuild; null-safe when MCM/settings aren't loaded (main menu, custom battle, tests).
    /// </summary>
    public static float GetScale() => Resolve(TaomSettings.Instance?.MapFigureScale);
}
